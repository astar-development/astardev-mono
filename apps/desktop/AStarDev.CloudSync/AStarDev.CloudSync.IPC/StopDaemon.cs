namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to shut down. It answers first, then stops gracefully, so interrupted operations are queued again at the next start.
/// With <paramref name="Restart"/> it exits with a non-zero code that makes the systemd user unit start it again after a few seconds; run any other way, it only stops.
/// </summary>
/// <param name="Restart"><see langword="true"/> to ask for a restart by the service manager, <see langword="false"/> to stop for good until someone starts the daemon.</param>
public sealed record StopDaemon(bool Restart) : ICommand;
