using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class TracesQueryShould
{
    private const string TraceA = "0af7651916cd43dd8448eb211c80319c";
    private const string TraceB = "11111111111111111111111111111111";

    private static readonly DateTimeOffset Noon = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static TelemetryEntry Span(DateTimeOffset start, string name, string traceId, string spanId, string parentSpanId, string status = "Unset", string detail = "")
        => new(TelemetryKind.Span, start, 0, name, $"{traceId}\t{spanId}\t{parentSpanId}\t12.5\t{status}\t{detail}");

    [Fact]
    public async Task return_the_spans_of_one_trace()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);
        await host.Services.GetRequiredService<ITelemetryStore>().AppendAsync(
            [
                Span(Noon, "ui.request", TraceA, "b7ad6b7169203331", ""),
                Span(Noon.AddSeconds(1), "daemon.handle", TraceA, "00f067aa0ba902b7", "b7ad6b7169203331", "Error", "Failed"),
                Span(Noon.AddSeconds(2), "other", TraceB, "2222222222222222", ""),
                new TelemetryEntry(TelemetryKind.Log, Noon, 2, "Sync", "not a span")
            ],
            Token);
        var query = host.Services.GetRequiredService<TracesQuery>();

        var one = await query.HandleAsync(new GetTraces(TraceId: TraceA), Token);
        var all = await query.HandleAsync(new GetTraces(), Token);
        var recent = await query.HandleAsync(new GetTraces(Since: Noon.AddSeconds(2)), Token);

        one.Value.ShouldBeOfType<Ok<TracesView>>().Value.Spans.ShouldBe(
            [
                new SpanView(TraceA, "00f067aa0ba902b7", "b7ad6b7169203331", "daemon.handle", Noon.AddSeconds(1), 12.5, true, "Failed"),
                new SpanView(TraceA, "b7ad6b7169203331", "", "ui.request", Noon, 12.5, false, "")
            ]);
        all.Value.ShouldBeOfType<Ok<TracesView>>().Value.Spans.Select(span => span.Name).ShouldBe(["other", "daemon.handle", "ui.request"]);
        recent.Value.ShouldBeOfType<Ok<TracesView>>().Value.Spans.Select(span => span.Name).ShouldBe(["other"]);
    }

    [Fact]
    public async Task leave_out_a_span_that_is_not_in_the_stored_shape()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);
        await host.Services.GetRequiredService<ITelemetryStore>().AppendAsync([new TelemetryEntry(TelemetryKind.Span, Noon, 0, "broken", "not a span")], Token);

        var result = await host.Services.GetRequiredService<TracesQuery>().HandleAsync(new GetTraces(), Token);

        result.Value.ShouldBeOfType<Ok<TracesView>>().Value.Spans.ShouldBeEmpty();
    }

    [Fact]
    public async Task fail_with_a_message_when_the_store_fails()
    {
        await using var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ITelemetryStore>().AddSingleton<ITelemetryStore, FailingTelemetryStore>());

        var result = await host.Services.GetRequiredService<TracesQuery>().HandleAsync(new GetTraces(), Token);

        result.Value.ShouldBeOfType<Fail<IpcError>>().Error.Value.ShouldBeOfType<HandlerFailed>().Message.ShouldContain("traces could not be read");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<TracesQuery>().HandleAsync(new GetTraces(), cancelled.Token));
    }
}
