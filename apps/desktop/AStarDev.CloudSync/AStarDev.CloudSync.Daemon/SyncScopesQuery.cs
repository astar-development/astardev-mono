using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetSyncScopes"/> from the <see cref="ISyncScopeStore"/>.</summary>
internal sealed class SyncScopesQuery(ISyncScopeStore store)
{
    /// <summary>Lists the folders one job synchronises.</summary>
    /// <returns>The scopes ordered by folder id, empty for an unknown job, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<SyncScopesView, IpcError>> HandleAsync(GetSyncScopes query, CancellationToken cancellationToken)
        => await store.ListAsync(new SyncJobId(query.SyncJobId), cancellationToken) switch
        {
            Ok<IReadOnlyList<SyncScope>> scopes => new Ok<SyncScopesView>(new SyncScopesView([.. scopes.Value.Select(scope => new SyncScopeView(scope.RemoteRootId.Value))])),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read", "folder selection")))
        };
}
