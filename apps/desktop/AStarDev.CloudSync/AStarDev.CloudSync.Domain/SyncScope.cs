namespace AStarDev.CloudSync.Domain;

/// <summary>A remote folder the user selected for synchronisation as part of one sync job. Nothing outside a job's scopes is synchronised.</summary>
/// <param name="SyncJobId">The job the folder belongs to.</param>
/// <param name="RemoteRootId">The provider's id for the selected folder. A job holds each folder at most once.</param>
/// <param name="Path">Where the folder sits from the drive root, which is also where it sits below the job's local root. A <see cref="RemoteRootId"/> of <c>root</c> covers the whole drive whatever the path; any other scope with a default or empty path covers nothing.</param>
public sealed record SyncScope(SyncJobId SyncJobId, RemoteItemId RemoteRootId, RelativePath Path = default);
