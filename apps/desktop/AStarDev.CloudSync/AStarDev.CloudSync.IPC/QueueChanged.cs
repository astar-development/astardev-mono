namespace AStarDev.CloudSync.IPC;

/// <summary>Published when the queue counts or the pending and running operations change. Delivered only to clients that called <see cref="IIpcClient.SubscribeAsync"/> and registered a handler.</summary>
/// <param name="Queue">The queue after the change, not a delta.</param>
public sealed record QueueChanged(QueueView Queue) : IEvent;
