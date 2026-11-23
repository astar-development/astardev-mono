namespace AStarDev.CloudSync.IPC;

/// <summary>Published when any value of the dashboard changes, including the queue counts. Delivered only to clients that called <see cref="IIpcClient.SubscribeAsync"/> and registered a handler.</summary>
/// <param name="Dashboard">The dashboard after the change, not a delta.</param>
public sealed record DashboardChanged(DashboardView Dashboard) : IEvent;
