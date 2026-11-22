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
        var client = new FakeIpcClient([], available: true) { DeadLettersReply = new Ok<DeadLettersView>(new DeadLettersView([dead])) };
        var viewModel = Create(client);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.DeadLetters.Count == 1);
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        viewModel.DeadLetters.ShouldBe([dead]);
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
    public async Task query_state_then_subscribe_on_connect()
    {
        var calls = new List<string>();
        var viewModel = Create(new FakeIpcClient(calls, available: true));
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = viewModel.RunAsync(source.Token);
        await WaitForAsync(() => viewModel.State == ConnectionState.Connected);
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        calls.ShouldBe(["connect", "query", "query", "query", "subscribe"]);
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
