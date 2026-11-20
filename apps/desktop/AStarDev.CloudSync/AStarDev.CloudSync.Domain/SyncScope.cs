namespace AStarDev.CloudSync.Domain;

/// <summary>A remote folder selected for synchronisation. S15 adds the job it belongs to.</summary>
public sealed record SyncScope(RemoteItemId RemoteRootId);
