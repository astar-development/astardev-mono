namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetDiagnostics"/>.</summary>
/// <param name="State">What the daemon is doing now.</param>
/// <param name="Version">The daemon's version text.</param>
/// <param name="StartedUtc">When the daemon started; subtract it from the current time to get the uptime.</param>
/// <param name="Checks">One entry per registered health check, ordered by name; empty when none is registered.</param>
public sealed record DiagnosticsView(DaemonState State, string Version, DateTimeOffset StartedUtc, IReadOnlyList<HealthCheckView> Checks);
