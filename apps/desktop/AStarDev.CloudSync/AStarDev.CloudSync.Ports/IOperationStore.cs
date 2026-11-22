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

    /// <summary>Lists every stored operation whatever its status.</summary>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The operations in claim order (priority, then age), or <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<Operation>, StoreError>> ListAsync(CancellationToken cancellationToken);
}
