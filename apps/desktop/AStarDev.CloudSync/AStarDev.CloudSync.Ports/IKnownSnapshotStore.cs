using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where the last reconciled state of each item is kept. Only the sync engine writes it, and only after an operation succeeded: a change is staged for an operation
/// and applied by <see cref="CommitAsync"/>. Failure is a <see cref="StoreError"/> value, never an exception; cancellation propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IKnownSnapshotStore
{
    /// <summary>Lists the baselines of a job.</summary>
    /// <param name="jobId">The job whose baselines to read.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>One entry per item that was ever reconciled; empty when none; or <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<KnownEntry>, StoreError>> ListAsync(SyncJobId jobId, CancellationToken cancellationToken);

    /// <summary>Records what the baseline of an item becomes once an operation succeeds. The baseline itself does not change yet.</summary>
    /// <param name="jobId">The job the operation belongs to.</param>
    /// <param name="operationId">The operation whose success applies the change. Staging again for the same operation replaces the earlier change.</param>
    /// <param name="itemId">The item whose baseline changes.</param>
    /// <param name="target">The new baseline; null removes the baseline, as after a delete.</param>
    /// <param name="cancellationToken">Cancelling before the write completes stages nothing.</param>
    /// <returns>Success, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> StageAsync(SyncJobId jobId, OperationId operationId, ItemId itemId, KnownSnapshot? target, CancellationToken cancellationToken);

    /// <summary>Applies the change staged for a succeeded operation and forgets it.</summary>
    /// <param name="operationId">The operation that succeeded.</param>
    /// <param name="cancellationToken">Cancelling before the write completes applies nothing.</param>
    /// <returns>Success, also when nothing was staged for the operation or it was already applied, so calling twice is safe; or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> CommitAsync(OperationId operationId, CancellationToken cancellationToken);
}
