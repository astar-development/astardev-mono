using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

internal sealed class FakeConflictStore(Result<int, StoreError> count) : IConflictStore
{
    public Task<Result<bool, StoreError>> AddAsync(Conflict conflict, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<IReadOnlyList<Conflict>, StoreError>> ListOpenAsync(SyncJobId jobId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<Page<Conflict>, StoreError>> PageOpenAsync(SyncJobId jobId, int skip, int take, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<Conflict, StoreError>> GetAsync(ConflictId id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<Unit, StoreError>> UpdateStatusAsync(ConflictId id, ConflictStatus status, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<int, StoreError>> CountOpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(count);
    }
}
