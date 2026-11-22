using System.Diagnostics.Metrics;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The sync metrics of doc 12: active jobs, items synchronised, open conflicts and conflict resolutions. The open conflict gauge reports the count last read from the conflict store by <see cref="RefreshOpenConflictsAsync"/>, so it is correct after a restart once refreshed and reading it never touches the database.</summary>
internal sealed class SyncMetrics : IDisposable
{
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
        Meter.CreateObservableGauge("cloudsync.sync.conflicts.open", () => Interlocked.Read(ref openConflicts), "{conflict}", "Conflicts waiting for the user, open or deferred, as last read from the conflict store.");
    }

    public static SyncMetrics Shared { get; } = new();

    public Meter Meter { get; }

    public void ItemsSynchronised(OperationType type) => itemsSynchronised.Add(1, new KeyValuePair<string, object?>("cloudsync.operation.type", type.ToString()));

    public void JobStarted() => activeJobs.Add(1);

    public void JobStopped() => activeJobs.Add(-1);

    public async Task<Result<Unit, StoreError>> RefreshOpenConflictsAsync(IConflictStore store, CancellationToken cancellationToken)
    {
        var counted = await store.CountOpenAsync(cancellationToken);

        return counted switch
        {
            Ok<int> ok => Store(ok.Value),
            Fail<StoreError> fail => fail
        };
    }

    public void ConflictResolved(string resolution)
    {
        conflictResolutions.Add(1, new KeyValuePair<string, object?>("cloudsync.conflict.resolution", resolution));
    }

    public void Dispose() => Meter.Dispose();

    private Result<Unit, StoreError> Store(int count)
    {
        Interlocked.Exchange(ref openConflicts, count);

        return new Ok<Unit>(default);
    }
}
