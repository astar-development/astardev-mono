namespace AStarDev.CloudSync.IPC;

public enum DaemonState
{
    Running,
    Stopping
}

/// <summary>Projection of the daemon state. Plain values only: IPC never exposes domain models.</summary>
public sealed record DaemonStatusView(DaemonState State, string Version, DateTimeOffset StartedAt);

public sealed record GetDaemonStatus : IQuery<DaemonStatusView>;

public sealed record DaemonStatusChanged(DaemonStatusView Status) : IEvent;
