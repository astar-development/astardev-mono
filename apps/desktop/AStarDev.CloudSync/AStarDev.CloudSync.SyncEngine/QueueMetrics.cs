using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The operation queue metrics of doc 12. The depth gauges report the counts last handed to <see cref="UpdateDepth"/>, so reading them never touches the database.</summary>
internal sealed class QueueMetrics : IDisposable
{
    private readonly Histogram<double> duration;
    private readonly Histogram<double> latency;
    private readonly Counter<long> completed;
    private readonly Counter<long> failed;
    private long pending;
    private long running;
    private long deadLettered;

    public QueueMetrics()
    {
        Meter = new Meter(ApplicationConstants.TelemetryName);
        Meter.CreateObservableGauge("cloudsync.queue.pending", () => Interlocked.Read(ref pending), "{operation}", "Operations waiting to run.");
        Meter.CreateObservableGauge("cloudsync.queue.running", () => Interlocked.Read(ref running), "{operation}", "Operations being run.");
        Meter.CreateObservableGauge("cloudsync.queue.dead_lettered", () => Interlocked.Read(ref deadLettered), "{operation}", "Operations given up on.");
        duration = Meter.CreateHistogram<double>("cloudsync.queue.operation.duration", "s", "How long an operation took to execute, whether it succeeded or failed.");
        latency = Meter.CreateHistogram<double>("cloudsync.queue.operation.latency", "s", "How long an operation waited between being enqueued and its first attempt starting.");
        completed = Meter.CreateCounter<long>("cloudsync.queue.operation.completed", "{operation}", "Operations that succeeded, by operation type.");
        failed = Meter.CreateCounter<long>("cloudsync.queue.operation.failed", "{operation}", "Operation attempts that failed, by operation type and error type; a retried operation counts once per failed attempt.");
    }

    public Meter Meter { get; }

    public void UpdateDepth(IReadOnlyDictionary<OperationStatus, int> counts)
    {
        Interlocked.Exchange(ref pending, counts.GetValueOrDefault(OperationStatus.Pending));
        Interlocked.Exchange(ref running, counts.GetValueOrDefault(OperationStatus.Running));
        Interlocked.Exchange(ref deadLettered, counts.GetValueOrDefault(OperationStatus.DeadLetter));
    }

    public void RecordDuration(TimeSpan elapsed) => duration.Record(elapsed.TotalSeconds);

    public void RecordStarted(Operation operation, DateTimeOffset now)
    {
        if (operation.Attempts <= 1) latency.Record(Math.Max(0, (now - operation.CreatedUtc).TotalSeconds), new KeyValuePair<string, object?>("cloudsync.operation.type", operation.Type.ToString()));
    }

    public void RecordCompleted(OperationType type) => completed.Add(1, new KeyValuePair<string, object?>("cloudsync.operation.type", type.ToString()));

    public void RecordFailed(OperationType type, string errorType) => failed.Add(1, new KeyValuePair<string, object?>("cloudsync.operation.type", type.ToString()), new KeyValuePair<string, object?>("error.type", errorType));

    public void Dispose() => Meter.Dispose();
}
