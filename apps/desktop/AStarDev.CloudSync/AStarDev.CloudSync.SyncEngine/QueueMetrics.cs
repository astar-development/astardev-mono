using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The operation queue metrics of doc 12. The depth gauges report the counts last handed to <see cref="UpdateDepth"/>, so reading them never touches the database.</summary>
internal sealed class QueueMetrics : IDisposable
{
    private readonly Meter meter = new(ApplicationConstants.TelemetryName);
    private readonly Histogram<double> duration;
    private long pending;
    private long running;
    private long deadLettered;

    public QueueMetrics()
    {
        meter.CreateObservableGauge("cloudsync.queue.pending", () => Interlocked.Read(ref pending), "{operation}", "Operations waiting to run.");
        meter.CreateObservableGauge("cloudsync.queue.running", () => Interlocked.Read(ref running), "{operation}", "Operations being run.");
        meter.CreateObservableGauge("cloudsync.queue.dead_lettered", () => Interlocked.Read(ref deadLettered), "{operation}", "Operations given up on.");
        duration = meter.CreateHistogram<double>("cloudsync.queue.operation.duration", "s", "How long an operation took to execute, whether it succeeded or failed.");
    }

    public void UpdateDepth(IReadOnlyDictionary<OperationStatus, int> counts)
    {
        Interlocked.Exchange(ref pending, counts.GetValueOrDefault(OperationStatus.Pending));
        Interlocked.Exchange(ref running, counts.GetValueOrDefault(OperationStatus.Running));
        Interlocked.Exchange(ref deadLettered, counts.GetValueOrDefault(OperationStatus.DeadLetter));
    }

    public void RecordDuration(TimeSpan elapsed) => duration.Record(elapsed.TotalSeconds);

    public void Dispose() => meter.Dispose();
}
