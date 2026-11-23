using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class TracesViewModelShould
{
    private const string Trace = "0af7651916cd43dd8448eb211c80319c";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static (Microsoft.Extensions.Hosting.IHost Host, TracesViewModel Screen) Create(FakeIpcClient client)
    {
        var host = OperationScreensHost.Create(client, new FixedTimeProvider(Now));

        return (host, host.Screen<TracesViewModel>());
    }

    [Fact]
    public async Task show_the_traces_from_the_daemon()
    {
        var client = new FakeIpcClient([], available: true)
        {
            TracesReply = _ => new Ok<TracesView>(new TracesView(
            [
                new SpanView(Trace, "00f067aa0ba902b7", "b7ad6b7169203331", "daemon.handle", Now, 12.5, true, "Failed"),
                new SpanView(Trace, "b7ad6b7169203331", "", "ui.request", Now.AddSeconds(-1), 30, false, "")
            ]))
        };
        var (host, screen) = Create(client);
        using var lifetime = host;

        await screen.RefreshAsync(Token);

        screen.Spans.Count.ShouldBe(2);
        screen.Spans[0].Header.ShouldContain("daemon.handle");
        screen.Spans[0].Header.ShouldContain("failed");
        screen.Spans[0].Ids.ShouldContain(Trace);
        screen.Spans[0].Detail.ShouldBe("Failed");
        screen.Spans[1].Header.ShouldContain("ui.request");
        screen.IsEmpty.ShouldBeFalse();
        screen.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task send_the_filters_to_the_daemon()
    {
        GetTraces? asked = null;
        var client = new FakeIpcClient([], available: true) { TracesReply = query => { asked = query; return new Ok<TracesView>(new TracesView([])); } };
        var (host, screen) = Create(client);
        using var lifetime = host;
        screen.WindowIndex = 1;
        screen.TraceId = $" {Trace} ";

        await screen.RefreshAsync(Token);

        asked.ShouldBe(new GetTraces(Now.AddHours(-1), Trace));
        screen.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task keep_the_spans_and_say_why_when_the_daemon_is_unreachable()
    {
        var reachable = new FakeIpcClient([], available: true) { TracesReply = _ => new Ok<TracesView>(new TracesView([new SpanView(Trace, "b7ad6b7169203331", "", "kept", Now, 1, false, "")])) };
        var (host, screen) = Create(reachable);
        using var lifetime = host;
        await screen.RefreshAsync(Token);
        reachable.TracesReply = _ => new Fail<IpcError>(new HandlerFailed("The traces could not be read because the database failed."));

        await screen.RefreshAsync(Token);

        screen.Spans.ShouldHaveSingleItem().Header.ShouldContain("kept");
        screen.ErrorMessage.ShouldBe("The traces could not be read because the database failed.");
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
