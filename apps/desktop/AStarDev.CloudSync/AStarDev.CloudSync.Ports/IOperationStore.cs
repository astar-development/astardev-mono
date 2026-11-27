using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// The durable operation queue. Failure is a <see cref="StoreError"/> value, never an exception; cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// Claims are atomic, so callers on any thread may claim at once without running two operations for one item.
/// </summary>
public interface IOperationStore
{
    /// <summary>Stores a new operation so it can be claimed.</summary>
    /// <param name="operation">The operation, normally <see cref="OperationStatus.Pending"/>. Its identity must not be stored already.</param>
    /// <param name="cancellationToken">Cancelling before the write completes stores nothing.</param>
    /// <returns>Success, <see cref="SyncJobNotFound"/> when the job is not stored, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> EnqueueAsync(Operation operation, CancellationToken cancellationToken);

    /// <summary>Claims up to <paramref name="limit"/> pending operations, marking each <see cref="OperationStatus.Running"/> and counting an attempt.</summary>
    /// <param name="nowUtc">Operations whose next attempt time is later than this are not due and are skipped.</param>
    /// <param name="limit">The most operations to claim. Zero or less claims none.</param>
    /// <param name="cancellationToken">Cancelling stops claiming; operations already claimed stay running.</param>
    /// <returns>
    /// The claimed operations in claim order: priority first, then age. An item that already has a running operation is skipped, so the list holds at most one operation per item.
    /// Empty when nothing is due. <see cref="StorageFailed"/> on a database failure.
    /// </returns>
    Task<Result<IReadOnlyList<Operation>, StoreError>> ClaimNextAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken);

    /// <summary>Marks a running operation <see cref="OperationStatus.Completed"/>, which frees its item for the next operation.</summary>
    /// <param name="id">The operation to complete.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the operation running.</param>
    /// <returns>Success, <see cref="OperationNotRunning"/> when it is missing or not running, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> CompleteAsync(OperationId id, CancellationToken cancellationToken);

    /// <summary>Records a failed attempt of a running operation and frees its item.</summary>
    /// <param name="id">The operation that failed.</param>
    /// <param name="error">Why it failed; kept as <see cref="Operation.LastError"/>.</param>
    /// <param name="retryAtUtc">When to try again, which puts the operation back to <see cref="OperationStatus.Pending"/>; <see langword="null"/> gives up and makes it <see cref="OperationStatus.DeadLetter"/>.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the operation running.</param>
    /// <returns>Success, <see cref="OperationNotRunning"/> when it is missing or not running, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> FailAsync(OperationId id, string error, DateTimeOffset? retryAtUtc, CancellationToken cancellationToken);

    /// <summary>Reads one page of the operations that gave up after their retries and are waiting for a decision.</summary>
    /// <param name="skip">How many operations to pass over, in claim order. Zero or more.</param>
    /// <param name="take">The most operations to return. At least one.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The page of <see cref="OperationStatus.DeadLetter"/> operations in claim order (priority, then age) with how many there are in all; an empty page with a total of zero when there are none. <see cref="StorageFailed"/> on a database failure.</returns>
    Task<Result<Page<Operation>, StoreError>> ListDeadLettersAsync(int skip, int take, CancellationToken cancellationToken);

    /// <summary>Puts a dead-lettered operation back to <see cref="OperationStatus.Pending"/> with its attempts reset to zero, so it is claimed again. Its last error is kept until the next failure replaces it.</summary>
    /// <param name="id">The operation to requeue.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the operation dead-lettered.</param>
    /// <returns>Success, <see cref="OperationNotDeadLettered"/> when it is missing or in another status, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> RequeueDeadLetterAsync(OperationId id, CancellationToken cancellationToken);

    /// <summary>Deletes a dead-lettered operation, so it is never run and no longer listed.</summary>
    /// <param name="id">The operation to drop.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the operation dead-lettered.</param>
    /// <returns>Success, <see cref="OperationNotDeadLettered"/> when it is missing or in another status, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> DiscardDeadLetterAsync(OperationId id, CancellationToken cancellationToken);

    /// <summary>Lists every stored operation whatever its status.</summary>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The operations in claim order (priority, then age), or <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<Operation>, StoreError>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Puts every operation left <see cref="OperationStatus.Running"/> back to <see cref="OperationStatus.Pending"/> for the daemon to run again after a restart. The attempt already counted stays counted and the next attempt time is kept, which is already past because the operation was claimed.</summary>
    /// <param name="reason">Kept as <see cref="Operation.LastError"/> of each requeued operation.</param>
    /// <param name="cancellationToken">Cancelling before the write completes requeues nothing.</param>
    /// <returns>How many operations were requeued, zero when none was running, or <see cref="StorageFailed"/>.</returns>
    Task<Result<int, StoreError>> RequeueRunningAsync(string reason, CancellationToken cancellationToken);

    /// <summary>Counts the stored operations of each status.</summary>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The count per status; a status with no operation is absent. <see cref="StorageFailed"/> on a database failure.</returns>
    Task<Result<IReadOnlyDictionary<OperationStatus, int>, StoreError>> CountByStatusAsync(CancellationToken cancellationToken);
}
