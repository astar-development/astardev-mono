using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetSyncStatus"/> from the operation store, the provider health check and the <see cref="ActivityTracker"/>, and publishes <see cref="SyncStatusChanged"/> when the answer changes.</summary>
internal sealed class SyncStatusQuery(IOperationStore operations, HealthCheckService health, ActivityTracker tracker, IIpcServer server, IDatabaseWrites writes)
{
    private readonly StoreReadCache<(HealthStatus Health, IReadOnlyDictionary<OperationStatus, int> Counts)> reads = new(writes);
    private readonly object gate = new();
    private Option<SyncStatusView> published = Option.None<SyncStatusView>();

    /// <summary>Reads the status bar values now.</summary>
    /// <returns>The status, or <see cref="HandlerFailed"/> carrying the queue failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<SyncStatusView, IpcError>> HandleAsync(GetSyncStatus query, CancellationToken cancellationToken)
        => ViewOf(await ReadAsync(cancellationToken));

    /// <summary>Publishes <see cref="SyncStatusChanged"/> when the status differs from the one published last. The first call always publishes. Skips the database reads while no write was committed since the last one; the <see cref="ActivityTracker"/> is read every call.</summary>
    /// <returns>Success when nothing needed publishing or the event went out; otherwise the read or publish failure. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<Unit, IpcError>> PublishIfChangedAsync(CancellationToken cancellationToken)
        => ViewOf(await reads.ReadAsync(ReadAsync, cancellationToken)) switch
        {
            Ok<SyncStatusView> status => HasChanged(status.Value) ? await server.PublishAsync(new SyncStatusChanged(status.Value), cancellationToken) : new Ok<Unit>(default),
            Fail<IpcError> fail => fail
        };

    private async Task<Result<(HealthStatus Health, IReadOnlyDictionary<OperationStatus, int> Counts), IpcError>> ReadAsync(CancellationToken cancellationToken)
    {
        var report = await health.CheckHealthAsync(registration => registration.Name == ApplicationConstants.ProviderHealthCheckName, cancellationToken);

        return await operations.CountByStatusAsync(cancellationToken) switch
        {
            Ok<IReadOnlyDictionary<OperationStatus, int>> counts => new Ok<(HealthStatus Health, IReadOnlyDictionary<OperationStatus, int> Counts)>((report.Status, counts.Value)),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read", "operation queue")))
        };
    }

    private Result<SyncStatusView, IpcError> ViewOf(Result<(HealthStatus Health, IReadOnlyDictionary<OperationStatus, int> Counts), IpcError> read) => read switch
    {
        Ok<(HealthStatus Health, IReadOnlyDictionary<OperationStatus, int> Counts)> ok => new Ok<SyncStatusView>(ViewOf(ok.Value.Health, ok.Value.Counts)),
        Fail<IpcError> fail => fail
    };

    private bool HasChanged(SyncStatusView status)
    {
        lock (gate)
        {
            if (published.Value is Some<SyncStatusView> last && last.Value == status) return false;

            published = Option.Some(status);

            return true;
        }
    }

    private SyncStatusView ViewOf(HealthStatus health, IReadOnlyDictionary<OperationStatus, int> counts)
        => new(ProviderHealthOf(health), counts.GetValueOrDefault(OperationStatus.Pending) + counts.GetValueOrDefault(OperationStatus.Running) + counts.GetValueOrDefault(OperationStatus.Failed), counts.GetValueOrDefault(OperationStatus.DeadLetter) + tracker.StoppedJobs(), tracker.Current().Activities.Select(activity => (DateTimeOffset?)activity.UpdatedUtc).Max());

    private static ProviderHealthState ProviderHealthOf(HealthStatus health) => health switch
    {
        HealthStatus.Healthy => ProviderHealthState.Healthy,
        HealthStatus.Degraded => ProviderHealthState.Degraded,
        _ => ProviderHealthState.Unhealthy
    };
}
