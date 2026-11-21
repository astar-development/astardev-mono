namespace AStarDev.CloudSync.IPC;

/// <summary>The folders one job synchronises, answering <see cref="GetSyncScopes"/>.</summary>
/// <param name="Scopes">Ordered by folder id. Empty when nothing is selected.</param>
public sealed record SyncScopesView(IReadOnlyList<SyncScopeView> Scopes);
