namespace AStarDev.CloudSync.IPC;

/// <summary>Published whenever the daemon has new progress for a job, a few times per second at most. Delivered only to clients that called <see cref="IIpcClient.SubscribeAsync"/> and registered a handler.</summary>
/// <param name="Activity">The job's activity now; it replaces the one with the same <see cref="ActivityView.SyncJobId"/>.</param>
public sealed record ActivityChanged(ActivityView Activity) : IEvent;
