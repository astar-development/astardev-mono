using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class TokenCacheShould
{
    private const string MissingTool = "astardev-no-such-secret-tool";

    [Fact]
    public async Task fail_with_a_clear_message_when_secret_service_is_unavailable()
    {
        var store = new SecretToolTokenCacheStore(MissingTool);

        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);
        var saved = await store.SaveAsync([1, 2, 3], TestContext.Current.CancellationToken);

        loaded.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>().Message.ShouldContain("Secret Service");
        saved.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>().Message.ShouldContain("Secret Service");
    }

    [Fact]
    public async Task fail_to_clear_when_secret_service_is_unavailable()
    {
        var store = new SecretToolTokenCacheStore(MissingTool);

        var cleared = await store.ClearAsync(TestContext.Current.CancellationToken);

        cleared.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>().Message.ShouldContain("Secret Service");
    }

    [Fact]
    public async Task never_fall_back_to_a_file()
    {
        var before = TokenFiles();
        var store = new SecretToolTokenCacheStore(MissingTool);

        var saved = await store.SaveAsync([1, 2, 3], TestContext.Current.CancellationToken);

        saved.Value.ShouldBeOfType<Fail<ProviderError>>();
        TokenFiles().ShouldBe(before);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var store = new SecretToolTokenCacheStore(MissingTool);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => store.LoadAsync(cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.SaveAsync([1], cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.ClearAsync(cancelled.Token));
    }

    private static string[] TokenFiles()
        => new[] { Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.ApplicationData }
            .Select(Environment.GetFolderPath)
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateDirectories(folder, "*astardev*"))
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            .Order()
            .ToArray();
}
