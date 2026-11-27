using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using AStarDev.CloudSync.UI.ViewModels;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ConnectionViewModelShould
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(100);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ConnectionViewModel Create(params FakeIpcClient[] clients)
    {
        var queue = new Queue<FakeIpcClient>(clients);
        return new ConnectionViewModel(() => queue.Count > 1 ? queue.Dequeue() : queue.Peek(), Delay, Delay);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5, Token);
        }

        condition().ShouldBeTrue();
    }

    [Fact]
    public async Task show_disconnected_when_the_daemon_is_unavailable()
    {
        var viewModel = Create(new FakeIpcClient([], available: false));
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.State == ConnectionState.Disconnected);
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task not_have_connected_while_the_daemon_is_unavailable()
    {
        var viewModel = Create(new FakeIpcClient([], available: false));
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.State == ConnectionState.Disconnected);
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        viewModel.HasConnected.ShouldBeFalse();
        viewModel.StartupTimedOut.ShouldBeFalse();
    }

    [Fact]
    public async Task time_out_startup_when_no_connect_happens_within_the_startup_timeout()
    {
        var viewModel = new ConnectionViewModel(() => new FakeIpcClient([], available: false), Delay, Delay, startupTimeout: TimeSpan.FromMilliseconds(150));
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.StartupTimedOut);
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        viewModel.HasConnected.ShouldBeFalse();
    }

    [Fact]
    public async Task have_connected_and_not_time_out_once_the_daemon_answers()
    {
        var viewModel = new ConnectionViewModel(() => new FakeIpcClient([], available: true), Delay, Delay, startupTimeout: TimeSpan.Zero);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.HasConnected);
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        viewModel.StartupTimedOut.ShouldBeFalse();
    }

    [Fact]
    public async Task load_the_activities_on_connect_and_follow_changes()
    {
        var started = new DateTimeOffset(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);
        var job = Guid.NewGuid();
        var first = new ActivityView(job, "OneDrive", ActivityState.InitialIndexing, 10, 100, 10, null, started, started, null, false);
        var client = new FakeIpcClient([], available: true) { ActivitiesReply = new Ok<ActivitiesView>(new ActivitiesView([first])) };
        var viewModel = Create(client);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.Activities.Count == 1);
        client.Raise(new ActivityChanged(first with { ItemsDone = 50, Percent = 50 }));
        client.Raise(new ActivityChanged(first with { SyncJobId = Guid.NewGuid(), Name = "Other" }));
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        viewModel.Activities.Select(activity => (activity.Name, activity.ItemsDone)).ShouldBe([("OneDrive", 50L), ("Other", 10L)]);
    }

    [Fact]
    public async Task load_the_dead_letters_on_connect()
    {
        var dead = new DeadLetterView(Guid.NewGuid(), Guid.NewGuid(), "UploadFile", 5, "access denied", DateTimeOffset.UnixEpoch);
        var client = new FakeIpcClient([], available: true) { DeadLettersReply = new Ok<DeadLettersView>(new DeadLettersView(1, [dead])) };
        var viewModel = Create(client);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.DeadLetters.Count == 1);
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        viewModel.DeadLetters.ShouldBe([dead]);
    }

    [Fact]
    public async Task load_the_queue_on_connect_and_follow_changes()
    {
        var queued = new QueuedOperationView(Guid.NewGuid(), "OneDrive", "UploadFile", "Pending", "a.txt", 12, DateTimeOffset.UnixEpoch);
        var client = new FakeIpcClient([], available: true) { QueueReply = new Ok<QueueView>(new QueueView(1, 0, 0, 0, [queued])) };
        var viewModel = Create(client);
        var seen = new List<IEvent>();
        viewModel.EventReceived += seen.Add;
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.Queue.Pending == 1);
        var changed = new QueueChanged(new QueueView(0, 1, 1, 0, [queued with { Status = "Running" }]));
        client.Raise(changed);
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        viewModel.Queue.ShouldBe(changed.Queue);
        seen.ShouldBe([changed]);
    }

    [Fact]
    public async Task reconnect_when_the_daemon_returns()
    {
        var first = new FakeIpcClient([], available: true);
        var viewModel = Create(first, new FakeIpcClient([], available: false), new FakeIpcClient([], available: true));
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.State == ConnectionState.Connected);
        first.Drop();
        await WaitForAsync(() => viewModel.State == ConnectionState.Disconnected);
        await WaitForAsync(() => viewModel.State == ConnectionState.Connected);
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task count_a_detected_conflict_beyond_the_loaded_pages_without_listing_it_and_not_count_a_resolution_twice()
    {
        var job = new SyncJobView(Guid.NewGuid(), Guid.NewGuid(), "A", "/a", SyncBootstrapMode.Merge);
        var stamp = new DateTimeOffset(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);
        var loaded = new ConflictView(Guid.NewGuid(), job.Id, Guid.NewGuid(), "ModifyModify", stamp, "Open", "a.txt", null, null);
        var detected = loaded with { ConflictId = Guid.NewGuid(), Path = "c.txt", DetectedUtc = stamp.AddHours(2) };
        var client = new FakeIpcClient([], available: true)
        {
            JobsReply = new Ok<SyncJobsView>(new SyncJobsView([job])),
            ConflictsReply = _ => new Ok<ConflictsView>(new ConflictsView(2, [loaded]))
        };
        var viewModel = Create(client);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.Conflicts.Count == 1);
        client.Raise(new ConflictDetected(detected));
        var afterDetected = (viewModel.Conflicts.Count, viewModel.ConflictsTotal);
        viewModel.ReplaceConflicts(new Dictionary<Guid, ConflictsView> { [job.Id] = new(2, [detected]) });
        client.Raise(new ConflictResolved(loaded.ConflictId, job.Id, loaded.ItemId, "KeepLocal", stamp.AddHours(3)));
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        afterDetected.ShouldBe((1, 3));
        viewModel.Conflicts.ShouldBe([detected]);
        viewModel.ConflictsTotal.ShouldBe(2);
    }

    [Fact]
    public async Task load_the_conflicts_of_every_job_on_connect_oldest_first_and_follow_changes()
    {
        var first = new SyncJobView(Guid.NewGuid(), Guid.NewGuid(), "A", "/a", SyncBootstrapMode.Merge);
        var second = new SyncJobView(Guid.NewGuid(), Guid.NewGuid(), "B", "/b", SyncBootstrapMode.Merge);
        var stamp = new DateTimeOffset(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);
        var newer = new ConflictView(Guid.NewGuid(), first.Id, Guid.NewGuid(), "ModifyModify", stamp.AddHours(1), "Open", "a.txt", null, null);
        var older = new ConflictView(Guid.NewGuid(), second.Id, Guid.NewGuid(), "KeepDelete", stamp, "Open", "b.txt", null, null);
        var client = new FakeIpcClient([], available: true)
        {
            JobsReply = new Ok<SyncJobsView>(new SyncJobsView([first, second])),
            ConflictsReply = query => new Ok<ConflictsView>(new ConflictsView(1, query.SyncJobId == first.Id ? [newer] : [older]))
        };
        var viewModel = Create(client);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.Conflicts.Count == 2);
        var detected = new ConflictView(Guid.NewGuid(), first.Id, Guid.NewGuid(), "CreateCreate", stamp.AddHours(2), "Open", "c.txt", null, null);
        client.Raise(new ConflictDetected(detected));
        client.Raise(new ConflictDetected(newer with { Status = "Deferred" }));
        client.Raise(new ConflictResolved(older.ConflictId, older.SyncJobId, older.ItemId, "KeepLocal", stamp.AddHours(3)));
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        viewModel.Conflicts.Select(conflict => (conflict.Path, conflict.Status)).ShouldBe([("a.txt", "Deferred"), ("c.txt", "Open")]);
    }

    [Fact]
    public async Task query_state_then_subscribe_on_connect()
    {
        var calls = new List<string>();
        var viewModel = Create(new FakeIpcClient(calls, available: true));
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.State == ConnectionState.Connected);
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        calls.ShouldBe(["connect", "query", "query", "query", "query", "query", "query", "subscribe"]);
        viewModel.DaemonStatus.ShouldBe(FakeIpcClient.Status);
    }

    [Fact]
    public async Task honour_cancellation_before_connecting()
    {
        var calls = new List<string>();
        var viewModel = Create(new FakeIpcClient(calls, available: true));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => viewModel.RunAsync(source.Token));

        calls.ShouldBeEmpty();
    }
}
