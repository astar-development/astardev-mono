namespace AStarDev.CloudSync.IPC;

/// <summary>The folders one job synchronises, answering <see cref="GetSyncScopes"/>.</summary>
/// <param name="Scopes">Ordered by folder id. Empty when nothing is selected.</param>
/// <param name="UnsyncedFolders">Names of the top-level folders in the job's local root that no scope covers, ordered by name. They are not uploaded or deleted; empty when every local folder is covered or the local root cannot be read.</param>
public sealed record SyncScopesView(IReadOnlyList<SyncScopeView> Scopes, IReadOnlyList<string> UnsyncedFolders);
