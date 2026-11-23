using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class PhysicalLocalFileStoreShould : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"cloudsync-store-{Guid.NewGuid():N}");
    private readonly PhysicalLocalFileStore store = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task write_a_file_with_the_given_modified_time_and_no_temporary_file_left()
    {
        var modified = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        await using var content = new MemoryStream("data"u8.ToArray());

        var result = await store.WriteAsync(root, new RelativePath("a/b.txt"), content, modified, Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        (await File.ReadAllTextAsync(Path.Combine(root, "a", "b.txt"), Token)).ShouldBe("data");
        File.GetLastWriteTimeUtc(Path.Combine(root, "a", "b.txt")).ShouldBe(modified.UtcDateTime);
        Directory.GetFiles(Path.Combine(root, "a")).Length.ShouldBe(1);
    }

    [Fact]
    public async Task fail_without_leaving_a_temporary_file_when_the_target_cannot_be_replaced()
    {
        Directory.CreateDirectory(Path.Combine(root, "a.txt"));
        await using var content = new MemoryStream("data"u8.ToArray());

        var result = await store.WriteAsync(root, new RelativePath("a.txt"), content, DateTimeOffset.UnixEpoch, Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Permanent>();
        Directory.GetFileSystemEntries(root).Select(Path.GetFileName).ShouldBe(["a.txt"]);
    }

    [Fact]
    public async Task refuse_a_path_that_leaves_the_root()
    {
        await using var content = new MemoryStream();

        var result = await store.WriteAsync(root, new RelativePath("../escape.txt"), content, DateTimeOffset.UnixEpoch, Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Permanent>();
        store.Exists(root, new RelativePath("../escape.txt")).ShouldBeFalse();
    }

    [Fact]
    public void set_the_help_link_on_a_path_that_escapes_the_root()
    {
        var thrown = Should.Throw<UnauthorizedAccessException>(() => PhysicalLocalFileStore.Inside(root, new RelativePath("../escape.txt")));

        thrown.HelpLink.ShouldBe(ErrorLinks.Root + "CS-SYNC-0012");
    }

    [Fact]
    public void report_a_locked_file_as_a_permanent_failure()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "a.txt");
        File.WriteAllText(path, "x");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        store.OpenRead(root, new RelativePath("a.txt")).Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Permanent>();
    }

    [Fact]
    public void not_replace_a_file_when_moving()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "a.txt"), "a");
        File.WriteAllText(Path.Combine(root, "b.txt"), "b");

        var result = store.Move(root, new RelativePath("a.txt"), new RelativePath("b.txt"));

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Permanent>();
        File.ReadAllText(Path.Combine(root, "b.txt")).ShouldBe("b");
    }

    [Fact]
    public void treat_a_repeated_move_and_delete_as_done()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "a.txt"), "a");

        store.Move(root, new RelativePath("a.txt"), new RelativePath("x/b.txt")).Value.ShouldBeOfType<Ok<Unit>>();
        store.Move(root, new RelativePath("a.txt"), new RelativePath("x/b.txt")).Value.ShouldBeOfType<Ok<Unit>>();
        store.Delete(root, new RelativePath("x/b.txt")).Value.ShouldBeOfType<Ok<Unit>>();
        store.Delete(root, new RelativePath("x/b.txt")).Value.ShouldBeOfType<Ok<Unit>>();
    }

    [Fact]
    public async Task honour_cancellation_and_leave_nothing_behind()
    {
        Directory.CreateDirectory(root);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await using var content = new MemoryStream("data"u8.ToArray());

        await Should.ThrowAsync<OperationCanceledException>(() => store.WriteAsync(root, new RelativePath("a.txt"), content, DateTimeOffset.UnixEpoch, cancelled.Token));

        Directory.GetFiles(root).ShouldBeEmpty();
    }
}
