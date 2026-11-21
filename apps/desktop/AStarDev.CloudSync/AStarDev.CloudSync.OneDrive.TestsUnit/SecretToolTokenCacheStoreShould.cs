using System.Runtime.Versioning;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

[SupportedOSPlatform("linux")]
public sealed class SecretToolTokenCacheStoreShould : IDisposable
{
    private const string FakeSecretTool = """
        #!/bin/bash
        command="$1"
        shift
        name="legacy"
        while [ $# -gt 0 ]; do
          if [ "$1" = "chunk" ]; then name="chunk-$2"; fi
          shift
        done
        case "$command" in
          store)
            value="$(cat)"
            if [ ${#value} -gt 8192 ]; then echo "secret-tool: password is too long" >&2; exit 1; fi
            printf '%s' "$value" > "$VAULT/$name"
            ;;
          lookup)
            if [ -f "$VAULT/$name" ]; then cat "$VAULT/$name"; else exit 1; fi
            ;;
          clear)
            if ls "$VAULT"/* >/dev/null 2>&1; then rm -f "$VAULT"/*; else exit 1; fi
            ;;
        esac
        """;

    private readonly string root = Path.Combine(Path.GetTempPath(), $"astardev-secret-tool-{Guid.NewGuid():N}");

    public SecretToolTokenCacheStoreShould()
    {
        Directory.CreateDirectory(Vault);
        var script = FakeSecretTool.Replace("$VAULT", Vault, StringComparison.Ordinal).ReplaceLineEndings("\n") + "\n";
        File.WriteAllText(Executable, script);
        File.SetUnixFileMode(Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private string Vault => Path.Combine(root, "vault");

    private string Executable => Path.Combine(root, "secret-tool");

    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public async Task round_trip_a_cache_larger_than_one_secret_entry()
    {
        var cache = RandomBytes(20000);
        var store = new SecretToolTokenCacheStore(Executable);

        var saved = await store.SaveAsync(cache, TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        saved.Value.ShouldBeOfType<Ok<Unit>>();
        loaded.Value.ShouldBeOfType<Ok<byte[]>>().Value.ShouldBe(cache);
        Directory.GetFiles(Vault).Length.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task round_trip_a_small_cache()
    {
        var store = new SecretToolTokenCacheStore(Executable);

        await store.SaveAsync([1, 2, 3], TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        loaded.Value.ShouldBeOfType<Ok<byte[]>>().Value.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task replace_a_previous_larger_cache_without_leaving_stale_chunks()
    {
        var store = new SecretToolTokenCacheStore(Executable);
        await store.SaveAsync(RandomBytes(30000), TestContext.Current.CancellationToken);
        var smaller = RandomBytes(100);

        await store.SaveAsync(smaller, TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        loaded.Value.ShouldBeOfType<Ok<byte[]>>().Value.ShouldBe(smaller);
        Directory.GetFiles(Vault).Length.ShouldBe(1);
    }

    [Fact]
    public async Task treat_a_legacy_single_entry_as_nothing_stored()
    {
        File.WriteAllText(Path.Combine(Vault, "legacy"), Convert.ToBase64String([9, 9, 9]));
        var store = new SecretToolTokenCacheStore(Executable);

        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        loaded.Value.ShouldBeOfType<Ok<byte[]>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task remove_a_legacy_single_entry_when_saving()
    {
        File.WriteAllText(Path.Combine(Vault, "legacy"), Convert.ToBase64String([9, 9, 9]));
        var store = new SecretToolTokenCacheStore(Executable);

        await store.SaveAsync([1, 2, 3], TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(Vault, "legacy")).ShouldBeFalse();
    }

    [Fact]
    public async Task return_nothing_stored_when_the_keyring_has_no_entry()
    {
        var store = new SecretToolTokenCacheStore(Executable);

        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        loaded.Value.ShouldBeOfType<Ok<byte[]>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task clear_every_chunk()
    {
        var store = new SecretToolTokenCacheStore(Executable);
        await store.SaveAsync(RandomBytes(20000), TestContext.Current.CancellationToken);

        var cleared = await store.ClearAsync(TestContext.Current.CancellationToken);

        cleared.Value.ShouldBeOfType<Ok<Unit>>();
        Directory.GetFiles(Vault).ShouldBeEmpty();
    }

    [Fact]
    public async Task fail_to_save_when_a_chunk_cannot_be_stored()
    {
        var script = File.ReadAllText(Executable).Replace("-gt 8192", "-gt 10", StringComparison.Ordinal);
        File.WriteAllText(Executable, script);
        var store = new SecretToolTokenCacheStore(Executable);

        var saved = await store.SaveAsync(RandomBytes(500), TestContext.Current.CancellationToken);

        saved.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
    }

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        new Random(42).NextBytes(bytes);

        return bytes;
    }
}
