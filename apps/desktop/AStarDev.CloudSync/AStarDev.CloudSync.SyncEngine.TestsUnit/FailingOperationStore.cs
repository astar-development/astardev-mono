using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

internal sealed class FailingOperationStore : IOperationStore
{
    private static readonly StoreError Down = new StorageFailed("store down");

    public Task<Result<Unit, StoreError>> EnqueueAsync(Operation operation, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<IReadOnlyList<Operation>, StoreError>> ClaimNextAsync(DateTimeOffset nowUtc, int limit, CancellationToken cancellationToken)
        => Task.FromResult<Result<IReadOnlyList<Operation>, StoreError>>(new Fail<StoreError>(Down));

    public Task<Result<Unit, StoreError>> CompleteAsync(OperationId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<Unit, StoreError>> FailAsync(OperationId id, string error, DateTimeOffset? retryAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<Page<Operation>, StoreError>> ListDeadLettersAsync(int skip, int take, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<Unit, StoreError>> RequeueDeadLetterAsync(OperationId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<Unit, StoreError>> DiscardDeadLetterAsync(OperationId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<IReadOnlyList<Operation>, StoreError>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<int, StoreError>> RequeueRunningAsync(string reason, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<IReadOnlyDictionary<OperationStatus, int>, StoreError>> CountByStatusAsync(CancellationToken cancellationToken)
        => Task.FromResult<Result<IReadOnlyDictionary<OperationStatus, int>, StoreError>>(new Fail<StoreError>(Down));

    public Task<Result<IReadOnlyList<Operation>, StoreError>> ListNewestAsync(IReadOnlyCollection<OperationStatus> statuses, int take, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<IReadOnlyDictionary<SyncJobId, IReadOnlyDictionary<OperationStatus, int>>, StoreError>> CountByJobAndStatusAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<int, StoreError>> PruneCompletedAsync(DateTimeOffset createdBeforeUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
}
