using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class MetricsViewModelShould
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static (Microsoft.Extensions.Hosting.IHost Host, MetricsViewModel Screen) Create(FakeIpcClient client)
    {
        var host = OperationScreensHost.Create(client, new FixedTimeProvider(Now));

        return (host, host.Screen<MetricsViewModel>());
    }

    [Fact]
    public async Task show_current_values_and_recent_history()
    {
        var client = new FakeIpcClient([], available: true)
        {
            MetricsReply = _ => new Ok<MetricsView>(new MetricsView(
            [
                new MetricSampleView("cloudsync.queue.pending", Now, 2.5, "{operation}"),
                new MetricSampleView("cloudsync.queue.running", Now, 1, "{operation}"),
                new MetricSampleView("cloudsync.queue.pending", Now.AddMinutes(-2), 4, "{operation}"),
                new MetricSampleView("cloudsync.queue.pending", Now.AddMinutes(-1), 3, "{operation}")
            ]))
        };
        var (host, screen) = Create(client);
        using var lifetime = host;

        await screen.RefreshAsync(Token);

        screen.Rows.Select(row => row.Name).ShouldBe(["cloudsync.queue.pending", "cloudsync.queue.running"]);
        screen.Rows[0].Current.ShouldBe("2.5 {operation}");
        screen.Rows[0].History.ShouldBe("4 3 2.5");
        screen.Rows[1].Current.ShouldBe("1 {operation}");
        screen.IsEmpty.ShouldBeFalse();
        screen.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task ask_for_the_last_hour()
    {
        GetMetrics? asked = null;
        var client = new FakeIpcClient([], available: true) { MetricsReply = query => { asked = query; return new Ok<MetricsView>(new MetricsView([])); } };
        var (host, screen) = Create(client);
        using var lifetime = host;

        await screen.RefreshAsync(Token);

        asked.ShouldBe(new GetMetrics(Now.AddHours(-1)));
        screen.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task keep_the_rows_and_say_why_when_the_daemon_is_unreachable()
    {
        var client = new FakeIpcClient([], available: true) { MetricsReply = _ => new Ok<MetricsView>(new MetricsView([new MetricSampleView("cloudsync.queue.pending", Now, 1, "{operation}")])) };
        var (host, screen) = Create(client);
        using var lifetime = host;
        await screen.RefreshAsync(Token);
        client.MetricsReply = _ => new Fail<IpcError>(new HandlerFailed("The metrics could not be read because the database failed: disk full"));

        await screen.RefreshAsync(Token);

        screen.Rows.Count.ShouldBe(1);
        screen.ErrorMessage.ShouldNotBeNull().ShouldContain("metrics could not be read");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var (host, screen) = Create(new FakeIpcClient([], available: true));
        using var lifetime = host;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => screen.RefreshAsync(cancelled.Token));
    }
}
