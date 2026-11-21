using System.Diagnostics;
using System.Diagnostics.Metrics;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC;

/// <summary>The spans and metrics recorded around every IPC request and event. Collected by name, so IPC needs no reference to Observability.</summary>
internal static class IpcTelemetry
{
    private const string RpcSystem = "cloudsync-ipc";

    private static readonly ActivitySource Source = new(ApplicationConstants.TelemetryName);
    private static readonly Meter Meter = new(ApplicationConstants.TelemetryName);
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("cloudsync.ipc.requests", "{request}", "Requests sent by the client or handled by the server, successful or not.");
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>("cloudsync.ipc.request.duration", "s", "How long a request took, from sending to answer on the client and from receipt to handler result on the server.");
    private static readonly UpDownCounter<long> Connections = Meter.CreateUpDownCounter<long>("cloudsync.ipc.connections", "{connection}", "Open IPC connections.");

    /// <summary>Starts the client span of a request. Its identity travels in the envelope so the daemon's span becomes its child.</summary>
    /// <param name="kind">The envelope kind: command, query or subscribe.</param>
    /// <param name="type">The message type name.</param>
    /// <returns>The span, or <see langword="null"/> when nothing is listening. Dispose it when the request ends.</returns>
    public static Activity? StartClientRequest(string kind, string type) => Start($"{kind} {type}", ActivityKind.Client, kind, type, default);

    /// <summary>Starts the server span of a request as a child of the span described by the envelope.</summary>
    /// <param name="kind">The envelope kind: command, query or subscribe.</param>
    /// <param name="type">The message type name.</param>
    /// <param name="envelope">The request, whose trace context is the parent. A missing or malformed context starts a new trace.</param>
    /// <returns>The span, or <see langword="null"/> when nothing is listening. Dispose it when the request ends.</returns>
    public static Activity? StartServerRequest(string kind, string type, TracedEnvelope envelope) => Start($"{kind} {type}", ActivityKind.Server, kind, type, ParentOf(envelope));

    /// <summary>Starts the span of an event being published. Its identity travels in the envelope so the subscriber's span becomes its child.</summary>
    /// <param name="type">The event type name.</param>
    /// <returns>The span, or <see langword="null"/> when nothing is listening. Dispose it when the event has been pushed.</returns>
    public static Activity? StartEventPublish(string type) => Start($"{ApplicationConstants.EventKind} {type}", ActivityKind.Producer, ApplicationConstants.EventKind, type, default);

    /// <summary>Starts the span of an event being handled by a subscriber, as a child of the publisher's span.</summary>
    /// <param name="envelope">The event, whose trace context is the parent.</param>
    /// <returns>The span, or <see langword="null"/> when nothing is listening. Dispose it when the handlers have run.</returns>
    public static Activity? StartEventReceive(EventEnvelope envelope) => Start($"{ApplicationConstants.EventKind} {envelope.Type}", ActivityKind.Consumer, ApplicationConstants.EventKind, envelope.Type, ParentOf(envelope));

    /// <summary>Copies the identity of <paramref name="activity"/> onto an envelope.</summary>
    /// <param name="envelope">The envelope to carry the context.</param>
    /// <param name="activity">The sending span. A <see langword="null"/> span leaves the envelope untouched.</param>
    /// <returns>The envelope with its trace context set, or unchanged when there is no span.</returns>
    public static TEnvelope Inject<TEnvelope>(TEnvelope envelope, Activity? activity) where TEnvelope : TracedEnvelope
        => activity is null ? envelope : envelope with { Traceparent = activity.Id, Tracestate = activity.TraceStateString };

    /// <summary>Counts one request, records how long it took and marks the span failed when it did not succeed.</summary>
    /// <param name="activity">The request's span, as returned by a start method.</param>
    /// <param name="side">Whether the client or the server recorded it.</param>
    /// <param name="kind">The envelope kind.</param>
    /// <param name="type">The message type name.</param>
    /// <param name="started">The <see cref="Stopwatch.GetTimestamp"/> value taken when the request began.</param>
    /// <param name="result">The outcome. A <see cref="Fail{TError}"/> sets the span status and the <c>error.type</c> tag to the <see cref="IpcError"/> case name.</param>
    public static void CompleteRequest<T>(Activity? activity, string side, string kind, string type, long started, Result<T, IpcError> result)
    {
        var tags = new TagList { { "rpc.system", RpcSystem }, { "rpc.method", type }, { "cloudsync.ipc.message.kind", kind }, { "cloudsync.ipc.side", side } };
        if (result is Fail<IpcError> fail)
        {
            var errorType = fail.Error.Value?.GetType().Name ?? nameof(IpcError);
            tags.Add("error.type", errorType);
            activity?.SetStatus(ActivityStatusCode.Error, fail.Error.Describe());
            activity?.SetTag("error.type", errorType);
        }

        Requests.Add(1, tags);
        RequestDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
    }

    /// <summary>Adds one to the open connection count of a side. Pair every call with <see cref="ConnectionClosed"/>.</summary>
    /// <param name="side">Whether the client or the server holds the connection.</param>
    public static void ConnectionOpened(string side) => Connections.Add(1, new KeyValuePair<string, object?>("cloudsync.ipc.side", side));

    /// <summary>Takes one off the open connection count of a side.</summary>
    /// <param name="side">Whether the client or the server held the connection.</param>
    public static void ConnectionClosed(string side) => Connections.Add(-1, new KeyValuePair<string, object?>("cloudsync.ipc.side", side));

    private static ActivityContext ParentOf(TracedEnvelope envelope)
        => ActivityContext.TryParse(envelope.Traceparent, envelope.Tracestate, isRemote: true, out var context) ? context : default;

    private static Activity? Start(string name, ActivityKind kind, string messageKind, string type, ActivityContext parent)
    {
        var activity = parent == default ? Source.StartActivity(name, kind) : Source.StartActivity(name, kind, parent);
        activity?.SetTag("rpc.system", RpcSystem);
        activity?.SetTag("rpc.method", type);
        activity?.SetTag("cloudsync.ipc.message.kind", messageKind);

        return activity;
    }
}
