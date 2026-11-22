using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The sync metrics of doc 12: active jobs, items synchronised, open conflicts and conflict resolutions. The open conflict count is tracked from what this process detects and resolves, so it starts at zero after a restart.</summary>
internal sealed class SyncMetrics : IDisposable
{
    private readonly object gate = new();
    private readonly Counter<long> itemsSynchronised;
    private readonly Counter<long> conflictResolutions;
    private readonly UpDownCounter<long> activeJobs;
    private long openConflicts;

    public SyncMetrics()
    {
        Meter = new Meter(ApplicationConstants.TelemetryName);
        itemsSynchronised = Meter.CreateCounter<long>("cloudsync.sync.items_synchronised", "{item}", "Items brought in step by a successful operation, by operation type.");
        conflictResolutions = Meter.CreateCounter<long>("cloudsync.sync.conflict.resolutions", "{conflict}", "Conflicts the user resolved, by resolution. Deferring is not a resolution.");
        activeJobs = Meter.CreateUpDownCounter<long>("cloudsync.sync.jobs.active", "{job}", "Sync jobs currently running in the background.");
        Meter.CreateObservableGauge("cloudsync.sync.conflicts.open", ReadOpenConflicts, "{conflict}", "Conflicts waiting for the user, as tracked by this process.");
    }

    public static SyncMetrics Shared { get; } = new();

    public Meter Meter { get; }

    public void ItemsSynchronised(OperationType type) => itemsSynchronised.Add(1, new KeyValuePair<string, object?>("cloudsync.operation.type", type.ToString()));

    public void JobStarted() => activeJobs.Add(1);

    public void JobStopped() => activeJobs.Add(-1);

    public void ConflictOpened()
    {
        lock (gate) openConflicts++;
    }

    public void ConflictResolved(string resolution)
    {
        lock (gate) openConflicts = Math.Max(0, openConflicts - 1);

        conflictResolutions.Add(1, new KeyValuePair<string, object?>("cloudsync.conflict.resolution", resolution));
    }

    public void Dispose() => Meter.Dispose();

    private long ReadOpenConflicts()
    {
        lock (gate) return openConflicts;
    }
}
