using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine.TestsUnit;

internal sealed class FakeScopeStore(params SyncScope[] scopes) : ISyncScopeStore
{
    public Task<Result<Unit, StoreError>> AddAsync(SyncScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<Unit, StoreError>> RemoveAsync(SyncScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Result<IReadOnlyList<SyncScope>, StoreError>> ListAsync(SyncJobId syncJobId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<Result<IReadOnlyList<SyncScope>, StoreError>>(new Ok<IReadOnlyList<SyncScope>>(scopes));
    }
}
