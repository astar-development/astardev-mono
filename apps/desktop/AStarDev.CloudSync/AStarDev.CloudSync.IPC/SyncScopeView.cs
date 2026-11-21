namespace AStarDev.CloudSync.IPC;

/// <summary>One remote folder a job synchronises.</summary>
/// <param name="RemoteRootId">The <see cref="RemoteFolderView.Id"/> of the selected folder.</param>
public sealed record SyncScopeView(string RemoteRootId);
