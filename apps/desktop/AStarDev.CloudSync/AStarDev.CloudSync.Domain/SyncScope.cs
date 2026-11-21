namespace AStarDev.CloudSync.Domain;

/// <summary>A remote folder the user selected for synchronisation as part of one sync job. Nothing outside a job's scopes is synchronised.</summary>
/// <param name="SyncJobId">The job the folder belongs to.</param>
/// <param name="RemoteRootId">The provider's id for the selected folder. A job holds each folder at most once.</param>
public sealed record SyncScope(SyncJobId SyncJobId, RemoteItemId RemoteRootId);
