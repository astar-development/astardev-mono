namespace AStarDev.CloudSync.IPC;

/// <summary>What the daemon is doing, as shown to the UI.</summary>
public enum DaemonState
{
    /// <summary>Startup finished; the daemon is serving requests.</summary>
    Running,

    /// <summary>Shutdown has begun.</summary>
    Stopping
}

/// <summary>Projection of the daemon state. Plain values only: IPC never exposes domain models.</summary>
/// <param name="State">What the daemon is doing now.</param>
/// <param name="Version">The daemon's version text, for display and support.</param>
/// <param name="StartedAt">When the daemon started, so the UI can show uptime.</param>
public sealed record DaemonStatusView(DaemonState State, string Version, DateTimeOffset StartedAt);

/// <summary>Asks for the current <see cref="DaemonStatusView"/>. Needs no parameters.</summary>
/// <example>
/// <code>
/// var status = await client.QueryAsync(new GetDaemonStatus(), cancellationToken);
/// </code>
/// </example>
public sealed record GetDaemonStatus : IQuery<DaemonStatusView>;

/// <summary>Published when the daemon's state changes. Delivered only to clients that called <see cref="IIpcClient.SubscribeAsync"/> and registered a handler.</summary>
/// <param name="Status">The status after the change, not a delta.</param>
/// <example>
/// <code>
/// client.OnEvent&lt;DaemonStatusChanged&gt;(changed => viewModel.State = changed.Status.State);
/// await client.SubscribeAsync(cancellationToken);
/// </code>
/// </example>
public sealed record DaemonStatusChanged(DaemonStatusView Status) : IEvent;
