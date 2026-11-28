using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

public sealed class LocalObserverShould
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);
    private static readonly DateTimeOffset Stamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly SyncJob Job = new(new SyncJobId(Guid.NewGuid()), new AccountId(Guid.NewGuid()), "job", "/root", new RemoteItemId("root"), BootstrapMode.Merge, true, Stamp, Stamp);

    private readonly FakeLocalFileSystem fileSystem = new();
    private readonly RecordingObservationStore store = new();
    private readonly ControlledTimeProvider time = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LocalObserver Observer() => Observer(new SyncScope(Job.Id, new RemoteItemId("root")));

    private LocalObserver Observer(params SyncScope[] scopes) => Observer(new LocalObservationOptions { Debounce = Debounce }, scopes);

    private LocalObserver Observer(LocalObservationOptions observation, params SyncScope[] scopes) => new(fileSystem, store, new FakeScopeStore(scopes), Options.Create(observation), time, NullLogger<LocalObserver>.Instance);

    private LocalObserver ObserverHashing(int parallelism) => Observer(new LocalObservationOptions { Debounce = Debounce, FingerprintParallelism = parallelism }, new SyncScope(Job.Id, new RemoteItemId("root")));

    [Fact]
    public async Task record_new_changed_and_deleted_files_in_one_scan()
    {
        fileSystem.Put("new.txt", 3, Stamp);
        fileSystem.Put("changed.txt", 9, Stamp.AddMinutes(1));
        store.Present.Add(new LocalSnapshot(new RelativePath("changed.txt"), 5, Stamp, null, new ContentFingerprint("old")));
        store.Present.Add(new LocalSnapshot(new RelativePath("gone.txt"), 1, Stamp, null, new ContentFingerprint("old")));

        var result = await Observer().ScanAsync(Job, default, Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        store.Recorded.SelectMany(recorded => recorded.Present).Select(snapshot => (snapshot.Path.Value, snapshot.Size, snapshot.Fingerprint!.Value.Value)).ShouldBe([("new.txt", 3, "fp-new.txt"), ("changed.txt", 9, "fp-changed.txt")], ignoreOrder: true);
        store.Recorded.SelectMany(recorded => recorded.Deleted).ShouldBe([new RelativePath("gone.txt")]);
    }

    [Fact]
    public async Task not_record_a_file_outside_every_scope()
    {
        fileSystem.Put("docs/a.txt", 1, Stamp);
        fileSystem.Put("stray/b.txt", 1, Stamp);

        var result = await Observer(new SyncScope(Job.Id, new RemoteItemId("docs-id"), new RelativePath("docs"))).ScanAsync(Job, default, Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        store.Recorded.SelectMany(recorded => recorded.Present).Select(snapshot => snapshot.Path.Value).ShouldBe(["docs/a.txt"]);
        fileSystem.Fingerprinted.ShouldBe(1);
    }

    [Fact]
    public async Task record_nothing_while_the_job_has_no_scope()
    {
        fileSystem.Put("a.txt", 1, Stamp);

        await Observer([]).ScanAsync(Job, default, Token);

        store.Recorded.ShouldBeEmpty();
    }

    [Fact]
    public async Task not_record_a_deletion_for_a_stored_file_outside_every_scope()
    {
        store.Present.Add(new LocalSnapshot(new RelativePath("stray/b.txt"), 1, Stamp, null, new ContentFingerprint("old")));

        await Observer(new SyncScope(Job.Id, new RemoteItemId("docs-id"), new RelativePath("docs"))).ScanAsync(Job, default, Token);

        store.Recorded.SelectMany(recorded => recorded.Deleted).ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation_when_reading_the_scopes()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => Observer().ScanAsync(Job, default, cancelled.Token));
    }

    [Fact]
    public async Task not_hash_or_record_a_file_whose_size_and_time_are_unchanged()
    {
        fileSystem.Put("same.txt", 5, Stamp);
        store.Present.Add(new LocalSnapshot(new RelativePath("same.txt"), 5, Stamp, null, new ContentFingerprint("stored")));

        await Observer().ScanAsync(Job, default, Token);

        fileSystem.Fingerprinted.ShouldBe(0);
        store.Recorded.ShouldBeEmpty();
    }

    [Fact]
    public async Task treat_a_time_that_differs_by_less_than_a_millisecond_as_unchanged()
    {
        fileSystem.Put("same.txt", 5, Stamp.AddTicks(4000));
        store.Present.Add(new LocalSnapshot(new RelativePath("same.txt"), 5, Stamp, null, new ContentFingerprint("stored")));

        await Observer().ScanAsync(Job, default, Token);

        store.Recorded.ShouldBeEmpty();
    }

    [Fact]
    public async Task scan_only_the_subtree_it_is_given()
    {
        fileSystem.Put("dir/a.txt", 1, Stamp);
        fileSystem.Put("other/b.txt", 1, Stamp);

        await Observer().ScanAsync(Job, new RelativePath("dir"), Token);

        store.Recorded.ShouldHaveSingleItem().Present.Select(snapshot => snapshot.Path.Value).ShouldBe(["dir/a.txt"]);
    }

    [Fact]
    public async Task merge_a_burst_of_events_into_one_scan_after_a_quiet_period()
    {
        await using var running = await StartAsync();
        fileSystem.Put("a.txt", 1, Stamp);
        for (var index = 0; index < 5; index++) fileSystem.Raise(new Touched(new RelativePath("a.txt")));
        await AdvanceUntilScannedAsync(2);

        fileSystem.Listed.ShouldBe(["", "a.txt"]);
        store.Recorded.ShouldHaveSingleItem().Present.Select(snapshot => snapshot.Path.Value).ShouldBe(["a.txt"]);
    }

    [Fact]
    public async Task extend_the_wait_while_events_keep_arriving()
    {
        await using var running = await StartAsync();
        fileSystem.Put("a.txt", 1, Stamp);
        fileSystem.Raise(new Touched(new RelativePath("a.txt")));
        await WaitForTimerAsync();
        fileSystem.Raise(new Touched(new RelativePath("a.txt")));

        time.Advance(Debounce);
        await WaitForTimerAsync();
        fileSystem.Listed.ShouldBe([""]);

        time.Advance(Debounce);
        await WaitForScansAsync(2);

        fileSystem.Listed.ShouldBe(["", "a.txt"]);
    }

    [Fact]
    public async Task rescan_everything_after_a_watcher_overflow()
    {
        await using var running = await StartAsync();
        fileSystem.Put("late.txt", 1, Stamp);
        fileSystem.Raise(new Overflowed());
        await WaitForTimerAsync();

        time.Advance(Debounce);
        await WaitForScansAsync(2);

        fileSystem.Listed.ShouldBe(["", ""]);
        store.Present.Select(snapshot => snapshot.Path.Value).ShouldBe(["late.txt"]);
    }

    [Fact]
    public async Task record_the_files_below_a_deleted_folder_as_deleted()
    {
        fileSystem.Put("dir/a.txt", 1, Stamp);
        fileSystem.Put("dir/b.txt", 1, Stamp);
        await using var running = await StartAsync();
        fileSystem.Remove("dir/a.txt");
        fileSystem.Remove("dir/b.txt");
        fileSystem.Raise(new Touched(new RelativePath("dir")));
        await WaitForTimerAsync();

        time.Advance(Debounce);
        await WaitForScansAsync(2);

        store.Present.ShouldBeEmpty();
    }

    [Fact]
    public async Task fail_when_the_root_cannot_be_watched()
    {
        fileSystem.WatchFailure = new FileSystemFailed("no such folder");

        var result = await Observer().RunAsync(Job, Token);

        result.Value.ShouldBeOfType<Fail<LocalObservationError>>().Error.Value.ShouldBeOfType<FileSystemFailed>();
    }

    [Fact]
    public async Task stop_with_cancellation_while_waiting_for_events()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var run = Observer().RunAsync(Job, cancellation.Token);
        while (!fileSystem.Watching) await Task.Delay(5, Token);
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () => await run);
        fileSystem.Watching.ShouldBeFalse();
    }

    [Fact]
    public async Task honour_a_token_that_is_already_cancelled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () => await Observer().ScanAsync(Job, default, cancellation.Token));
    }

    [Fact]
    public void hash_two_files_at_once_by_default()
        => new LocalObservationOptions().FingerprintParallelism.ShouldBe(2);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(16, 4)]
    public async Task hash_at_most_the_configured_number_of_files_at_once_and_never_more_than_four(int configured, int expected)
    {
        for (var index = 0; index < 12; index++) fileSystem.Put($"f{index}.txt", 1, Stamp);
        fileSystem.Hashing = (_, token) => Task.Delay(20, token);

        await ObserverHashing(configured).ScanAsync(Job, default, Token);

        fileSystem.MostHashingAtOnce.ShouldBe(expected);
        fileSystem.Fingerprinted.ShouldBe(12);
    }

    [Fact]
    public async Task record_hashed_files_in_disk_order_whichever_hash_finishes_first()
    {
        var names = Enumerable.Range(0, 20).Select(index => $"f{index:D2}.txt").ToList();
        foreach (var name in names) fileSystem.Put(name, 1, Stamp);
        fileSystem.Hashing = (path, token) => Task.Delay(names.Count - names.IndexOf(path.Value), token);

        await ObserverHashing(4).ScanAsync(Job, default, Token);

        store.Recorded.SelectMany(recorded => recorded.Present).Select(snapshot => (snapshot.Path.Value, snapshot.Fingerprint!.Value.Value)).ShouldBe(names.Select(name => (name, "fp-" + name)));
    }

    [Fact]
    public async Task stop_hashing_in_parallel_when_cancelled_and_record_nothing()
    {
        for (var index = 0; index < 8; index++) fileSystem.Put($"f{index}.txt", 1, Stamp);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        fileSystem.Hashing = (_, token) => Task.Delay(Timeout.Infinite, token);

        var scan = ObserverHashing(4).ScanAsync(Job, default, cancellation.Token);
        while (fileSystem.Fingerprinted < 4) await Task.Delay(5, Token);
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () => await scan);
        fileSystem.Fingerprinted.ShouldBe(4);
        store.Recorded.ShouldBeEmpty();
    }

    private async Task<Running> StartAsync()
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var task = Observer().RunAsync(Job, cancellation.Token);
        await WaitForScansAsync(1);

        return new Running(cancellation, task);
    }

    private async Task WaitForScansAsync(int total)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while ((fileSystem.Listed.Count < total || !fileSystem.Watching) && DateTimeOffset.UtcNow < deadline) await Task.Delay(5, Token);
        await Task.Delay(50, Token);
    }

    private async Task AdvanceUntilScannedAsync(int total)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (fileSystem.Listed.Count < total && DateTimeOffset.UtcNow < deadline)
        {
            if (time.PendingTimers > 0) time.Advance(Debounce);
            await Task.Delay(5, Token);
        }

        await Task.Delay(50, Token);
    }

    private async Task WaitForTimerAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (time.PendingTimers == 0 && DateTimeOffset.UtcNow < deadline) await Task.Delay(5, Token);
        time.PendingTimers.ShouldBeGreaterThan(0);
    }

    private sealed class Running(CancellationTokenSource cancellation, Task<Result<Unit, LocalObservationError>> task) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await cancellation.CancelAsync();
            await Task.WhenAny(task);
            cancellation.Dispose();
        }
    }
}
