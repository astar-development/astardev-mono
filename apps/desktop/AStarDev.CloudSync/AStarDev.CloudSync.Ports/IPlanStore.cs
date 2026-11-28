using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Writes what one planning batch decided in a single transaction, so planning many items costs one commit instead of several per item. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IPlanStore
{
    /// <summary>Removes the forgotten baselines, stages each work's baseline, queues its operations and stores each conflict with its journal entry, all or nothing.</summary>
    /// <param name="batch">What to write. An empty batch writes nothing.</param>
    /// <param name="cancellationToken">Cancelling before the commit writes nothing of the batch.</param>
    /// <returns>
    /// The ids of the conflicts that were stored, in batch order; a conflict whose item already has an open or deferred conflict of the same type is skipped together with its journal entry.
    /// <see cref="SyncJobNotFound"/> when the batch queues work or conflicts for a job that is not stored, or <see cref="StorageFailed"/>; either way nothing of the batch is written.
    /// </returns>
    /// <example>
    /// <code>
    /// var batch = new PlanBatch(jobId, [goneItemId], [new PlannedWork([download], targetBaseline)], [new PlannedConflict(conflict, detectedEntry)]);
    /// var stored = await planStore.SaveAsync(batch, cancellationToken);
    /// </code>
    /// </example>
    Task<Result<IReadOnlyList<ConflictId>, StoreError>> SaveAsync(PlanBatch batch, CancellationToken cancellationToken);
}
