using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where unresolved conflicts are kept until the user resolves them. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IConflictStore
{
    /// <summary>Stores a conflict unless the item already has an open conflict of the same type.</summary>
    /// <param name="conflict">The conflict to store; normally <see cref="ConflictStatus.Open"/>.</param>
    /// <param name="cancellationToken">Cancelling before the write completes stores nothing.</param>
    /// <returns><see langword="true"/> when stored, <see langword="false"/> when an open conflict of the same item and type exists so nothing was written, <see cref="SyncJobNotFound"/> when the job is not stored, or <see cref="StorageFailed"/>.</returns>
    Task<Result<bool, StoreError>> AddAsync(Conflict conflict, CancellationToken cancellationToken);

    /// <summary>Lists the conflicts of a job that wait for the user.</summary>
    /// <param name="jobId">The job whose conflicts to read.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The <see cref="ConflictStatus.Open"/> conflicts, oldest first; empty when none; or <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<Conflict>, StoreError>> ListOpenAsync(SyncJobId jobId, CancellationToken cancellationToken);
}
