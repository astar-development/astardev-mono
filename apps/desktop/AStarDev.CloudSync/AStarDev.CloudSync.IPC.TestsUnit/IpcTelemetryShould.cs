using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC.TestsUnit;

[Collection("IPC metrics")]
public sealed class IpcTelemetryShould : IAsyncLifetime
{
    private const string TelemetryName = "AStarDev.CloudSync.IPC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly DaemonStatusView Status = new(DaemonState.Running, "1.2.3", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

    private static readonly System.Text.Json.JsonElement EmptyPayload = System.Text.Json.JsonDocument.Parse("{}").RootElement.Clone();

    private sealed record ProbeQuery : IQuery<DaemonStatusView>;

    private sealed record FailingQuery : IQuery<DaemonStatusView>;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly string socketPath = Path.Combine(Path.GetTempPath(), $"cloudsync-ipc-telemetry-{Guid.NewGuid():N}.sock");
    private readonly IIpcServer server;
    private readonly IIpcClient client;

    public IpcTelemetryShould()
    {
        server = UnixSocketIpc.CreateServer(socketPath);
        client = UnixSocketIpc.CreateClient(socketPath);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await client.DisposeAsync();
        await server.DisposeAsync();
        File.Delete(socketPath);
    }

    private async Task StartAndConnectAsync()
    {
        server.MapQuery((ProbeQuery _, CancellationToken _) => Task.FromResult<Result<DaemonStatusView, IpcError>>(new Ok<DaemonStatusView>(Status)));
        server.MapQuery((FailingQuery _, CancellationToken _) => Task.FromResult<Result<DaemonStatusView, IpcError>>(new Fail<IpcError>(new HandlerFailed("nope"))));
        (await server.StartAsync(Token)).ShouldSucceed();
        (await client.ConnectAsync(Token)).ShouldSucceed();
    }

    private static ActivityListener Listen(ActivitySource testSource, List<Activity> spans, Action<Activity>? onStopped = null)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TelemetryName || source == testSource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.Source.Name != TelemetryName) return;

                lock (spans) spans.Add(activity);
                onStopped?.Invoke(activity);
            }
        };
        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    private static List<Activity> SpansOf(List<Activity> spans, ActivityTraceId traceId)
    {
        lock (spans) return spans.Where(span => span.TraceId == traceId).ToList();
    }

    private sealed record Measurement(string Instrument, double Value, Dictionary<string, object?> Tags);

    private static MeterListener Measure(List<Measurement> measurements)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == TelemetryName) meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(measurements, instrument, value, tags));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(measurements, instrument, value, tags));
        listener.Start();

        return listener;
    }

    private static void Add(List<Measurement> measurements, Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var entry = new Measurement(instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value));
        lock (measurements) measurements.Add(entry);
    }

    [Fact]
    public async Task start_a_span_per_query_named_after_the_message()
    {
        var spans = new List<Activity>();
        using var testSource = new ActivitySource(Guid.NewGuid().ToString("N"));
        using var listener = Listen(testSource, spans);
        using var testRoot = testSource.StartActivity("test");
        await StartAndConnectAsync();

        (await client.QueryAsync(new ProbeQuery(), Token).WaitAsync(Timeout, Token)).ShouldSucceed();

        var observed = SpansOf(spans, testRoot!.TraceId);
        var clientSpan = observed.Single(span => span.Kind == ActivityKind.Client);
        var serverSpan = observed.Single(span => span.Kind == ActivityKind.Server);
        clientSpan.DisplayName.ShouldBe("query ProbeQuery");
        serverSpan.DisplayName.ShouldBe("query ProbeQuery");
        serverSpan.GetTagItem("rpc.method").ShouldBe("ProbeQuery");
        serverSpan.Status.ShouldNotBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task propagate_trace_context_across_the_envelope()
    {
        var spans = new List<Activity>();
        using var testSource = new ActivitySource(Guid.NewGuid().ToString("N"));
        using var listener = Listen(testSource, spans);
        using var testRoot = testSource.StartActivity("test");
        await StartAndConnectAsync();

        (await client.QueryAsync(new ProbeQuery(), Token).WaitAsync(Timeout, Token)).ShouldSucceed();

        var observed = SpansOf(spans, testRoot!.TraceId);
        var clientSpan = observed.Single(span => span.Kind == ActivityKind.Client);
        var serverSpan = observed.Single(span => span.Kind == ActivityKind.Server);
        clientSpan.ParentSpanId.ShouldBe(testRoot.SpanId);
        serverSpan.TraceId.ShouldBe(clientSpan.TraceId);
        serverSpan.ParentSpanId.ShouldBe(clientSpan.SpanId);
    }

    [Fact]
    public async Task count_requests_and_record_duration()
    {
        var measurements = new List<Measurement>();
        using var listener = Measure(measurements);
        await StartAndConnectAsync();

        (await client.QueryAsync(new ProbeQuery(), Token).WaitAsync(Timeout, Token)).ShouldSucceed();

        List<Measurement> probe;
        lock (measurements) probe = measurements.Where(measurement => (string?)measurement.Tags.GetValueOrDefault("rpc.method") == "ProbeQuery").ToList();
        foreach (var side in new[] { "client", "server" })
        {
            probe.Single(measurement => measurement.Instrument == "cloudsync.ipc.requests" && (string?)measurement.Tags["cloudsync.ipc.side"] == side).Value.ShouldBe(1);
            probe.Single(measurement => measurement.Instrument == "cloudsync.ipc.request.duration" && (string?)measurement.Tags["cloudsync.ipc.side"] == side).Value.ShouldBeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task mark_the_span_and_the_metric_as_failed_when_the_handler_fails()
    {
        var spans = new List<Activity>();
        var measurements = new List<Measurement>();
        using var testSource = new ActivitySource(Guid.NewGuid().ToString("N"));
        using var listener = Listen(testSource, spans);
        using var meterListener = Measure(measurements);
        using var testRoot = testSource.StartActivity("test");
        await StartAndConnectAsync();

        (await client.QueryAsync(new FailingQuery(), Token).WaitAsync(Timeout, Token)).ShouldFail();

        var observed = SpansOf(spans, testRoot!.TraceId);
        observed.Count.ShouldBe(2);
        observed.ShouldAllBe(span => span.Status == ActivityStatusCode.Error);
        lock (measurements)
        {
            var requests = measurements.Where(measurement => measurement.Instrument == "cloudsync.ipc.requests" && (string?)measurement.Tags.GetValueOrDefault("rpc.method") == "FailingQuery").ToList();
            requests.Count.ShouldBe(2);
            requests.ShouldAllBe(measurement => (string?)measurement.Tags["error.type"] == "HandlerFailed");
        }
    }

    [Fact]
    public async Task start_a_span_per_event_on_both_sides_of_the_connection()
    {
        var spans = new List<Activity>();
        var bothStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var testSource = new ActivitySource(Guid.NewGuid().ToString("N"));
        ActivityTraceId? testTrace = null;
        using var listener = Listen(testSource, spans, _ =>
        {
            lock (spans)
            {
                var events = spans.Where(span => span.TraceId == testTrace && span.DisplayName == "event DaemonStatusChanged").Select(span => span.Kind).ToList();
                if (events.Contains(ActivityKind.Producer) && events.Contains(ActivityKind.Consumer)) bothStopped.TrySetResult();
            }
        });
        using var testRoot = testSource.StartActivity("test");
        testTrace = testRoot!.TraceId;
        client.OnEvent<DaemonStatusChanged>(_ => { });
        await StartAndConnectAsync();
        (await client.SubscribeAsync(Token)).ShouldSucceed();

        (await server.PublishAsync(new DaemonStatusChanged(Status), Token)).ShouldSucceed();
        await bothStopped.Task.WaitAsync(Timeout, Token);

        var observed = SpansOf(spans, testRoot!.TraceId).Where(span => span.DisplayName == "event DaemonStatusChanged").ToList();
        var producer = observed.Single(span => span.Kind == ActivityKind.Producer);
        var consumer = observed.Single(span => span.Kind == ActivityKind.Consumer);
        consumer.ParentSpanId.ShouldBe(producer.SpanId);
    }

    [Fact]
    public async Task count_open_connections_on_both_sides()
    {
        var measurements = new List<Measurement>();
        using var listener = Measure(measurements);
        await StartAndConnectAsync();

        await client.DisposeAsync();
        await client.Disconnected.WaitAsync(Timeout, Token);

        foreach (var side in new[] { "client", "server" })
        {
            await WaitForAsync(() => Connections(measurements, side, 1) && Connections(measurements, side, -1));
        }
    }

    [Fact]
    public void leave_the_trace_fields_out_of_an_envelope_without_trace_context()
    {
        var bytes = IpcJson.Encode(new QueryEnvelope(1, "ProbeQuery", EmptyPayload)).ShouldSucceed();
        var json = Encoding.UTF8.GetString(bytes);

        json.ShouldNotContain("traceparent");
        json.ShouldNotContain("tracestate");
    }

    [Fact]
    public void round_trip_trace_context_in_an_envelope()
    {
        var sent = new QueryEnvelope(1, "ProbeQuery", EmptyPayload) { Traceparent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", Tracestate = "vendor=value" };

        var received = IpcJson.Decode(IpcJson.Encode(sent).ShouldSucceed()).ShouldSucceed();

        var query = received.ShouldBeOfType<QueryEnvelope>();
        query.Traceparent.ShouldBe(sent.Traceparent);
        query.Tracestate.ShouldBe(sent.Tracestate);
    }

    private static bool Connections(List<Measurement> measurements, string side, double delta)
    {
        lock (measurements) return measurements.Any(measurement => measurement.Instrument == "cloudsync.ipc.connections" && measurement.Value == delta && (string?)measurement.Tags["cloudsync.ipc.side"] == side);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline);
            await Task.Delay(20, Token);
        }
    }
}
