using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Read side of the item index for reconciliation. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IItemStateStore
{
    /// <summary>Lists the files of a job with their latest local and remote observation. Folders are not listed.</summary>
    /// <param name="jobId">The job whose items to read.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>One row per stored file, with the stored <see cref="LocalSnapshot.Identity"/> and <see cref="StoredItem.LastLocalIdentity"/>; empty when none; or <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<StoredItem>, StoreError>> ListAsync(SyncJobId jobId, CancellationToken cancellationToken);
}
