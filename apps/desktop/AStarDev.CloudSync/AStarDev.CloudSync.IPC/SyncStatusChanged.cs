namespace AStarDev.CloudSync.IPC;

/// <summary>Published when any value of the status bar changes. Delivered only to clients that called <see cref="IIpcClient.SubscribeAsync"/> and registered a handler.</summary>
/// <param name="Status">The status after the change, not a delta.</param>
public sealed record SyncStatusChanged(SyncStatusView Status) : IEvent;
