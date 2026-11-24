using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetSyncScopes"/> from the <see cref="ISyncScopeStore"/> and the job's local root.</summary>
internal sealed class SyncScopesQuery(ISyncScopeStore store, ISyncJobStore jobs)
{
    /// <summary>Lists the folders one job synchronises and the local folders it does not.</summary>
    /// <returns>The scopes ordered by folder id and the unsynced local top-level folders, both empty for an unknown job, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. A local root that cannot be read lists no unsynced folders. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<SyncScopesView, IpcError>> HandleAsync(GetSyncScopes query, CancellationToken cancellationToken)
        => await store.ListAsync(new SyncJobId(query.SyncJobId), cancellationToken) switch
        {
            Ok<IReadOnlyList<SyncScope>> scopes => await ViewAsync(query, scopes.Value, cancellationToken),
            Fail<StoreError> fail => Failed(fail.Error)
        };

    private async Task<Result<SyncScopesView, IpcError>> ViewAsync(GetSyncScopes query, IReadOnlyList<SyncScope> scopes, CancellationToken cancellationToken)
        => await jobs.GetAsync(new SyncJobId(query.SyncJobId), cancellationToken) switch
        {
            Ok<SyncJob> job => new Ok<SyncScopesView>(ViewOf(scopes, UnsyncedFolders(job.Value.LocalRootPath, scopes))),
            Fail<StoreError> { Error.Value: SyncJobNotFound } => new Ok<SyncScopesView>(ViewOf(scopes, [])),
            Fail<StoreError> fail => Failed(fail.Error)
        };

    private static SyncScopesView ViewOf(IReadOnlyList<SyncScope> scopes, IReadOnlyList<string> unsynced)
        => new([.. scopes.Select(scope => new SyncScopeView(scope.RemoteRootId.Value))], unsynced);

    private static Fail<IpcError> Failed(StoreError error) => new(new HandlerFailed(StoreErrorMessages.Describe(error, "read", "folder selection")));

    private static IReadOnlyList<string> UnsyncedFolders(string localRoot, IReadOnlyList<SyncScope> scopes)
        => Try.Run(() => Directory.EnumerateDirectories(localRoot).Select(Path.GetFileName).OfType<string>().ToList()) switch
        {
            Success<List<string>> names => [.. names.Value.Where(name => IsUnsynced(scopes, new RelativePath(name))).Order(StringComparer.OrdinalIgnoreCase)],
            Failure<List<string>> => []
        };

    private static bool IsUnsynced(IReadOnlyList<SyncScope> scopes, RelativePath folder)
        => !ScopeCoverage.Covers(scopes, folder) && !ScopeCoverage.Touches(scopes, folder);
}
