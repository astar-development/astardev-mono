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

    /// <summary>Reads one conflict whatever its status.</summary>
    /// <param name="id">The conflict to read.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The conflict, <see cref="ConflictNotFound"/> when none has the identity, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Conflict, StoreError>> GetAsync(ConflictId id, CancellationToken cancellationToken);

    /// <summary>Moves a conflict to a new status. A conflict that is no longer <see cref="ConflictStatus.Open"/> no longer appears in <see cref="ListOpenAsync"/>.</summary>
    /// <param name="id">The conflict to change.</param>
    /// <param name="status">The status it takes. Setting the status it already has is not an error.</param>
    /// <param name="cancellationToken">Cancelling before the write completes changes nothing.</param>
    /// <returns>Success, <see cref="ConflictNotFound"/> when none has the identity, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> UpdateStatusAsync(ConflictId id, ConflictStatus status, CancellationToken cancellationToken);
}
