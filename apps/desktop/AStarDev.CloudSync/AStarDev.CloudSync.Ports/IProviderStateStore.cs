using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// What is kept about how far each sync job's provider bookmark has got. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IProviderStateStore
{
    /// <summary>Lists when each sync job last completed a sync.</summary>
    /// <param name="cancellationToken">Cancelling abandons the listing.</param>
    /// <returns>One entry per job that has synced at least once; a job that never has is absent, as is every job when nothing has synced yet. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyDictionary<SyncJobId, DateTimeOffset>, StoreError>> ListLastSyncAsync(CancellationToken cancellationToken);
}
