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

        calls.ShouldBe(["connect", "query", "subscribe"]);
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
