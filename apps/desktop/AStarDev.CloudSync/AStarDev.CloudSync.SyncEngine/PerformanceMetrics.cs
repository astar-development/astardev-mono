using System.Diagnostics.Metrics;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The performance metrics of doc 12 that the engine owns: how long reconciling a job and processing the deltas of a scope take.</summary>
internal sealed class PerformanceMetrics : IDisposable
{
    private readonly Histogram<double> reconciliation;
    private readonly Histogram<double> delta;

    public PerformanceMetrics()
    {
        Meter = new Meter(ApplicationConstants.TelemetryName);
        reconciliation = Meter.CreateHistogram<double>("cloudsync.performance.reconciliation.duration", "s", "How long planning one job took, from reading its state to queueing its operations and conflicts.");
        delta = Meter.CreateHistogram<double>("cloudsync.performance.delta.duration", "s", "How long applying the delta pages of one scope took, whether it succeeded or failed.");
    }

    public static PerformanceMetrics Shared { get; } = new();

    public Meter Meter { get; }

    public void RecordReconciliation(TimeSpan elapsed) => reconciliation.Record(elapsed.TotalSeconds);

    public void RecordDelta(TimeSpan elapsed) => delta.Record(elapsed.TotalSeconds);

    public void Dispose() => Meter.Dispose();
}
