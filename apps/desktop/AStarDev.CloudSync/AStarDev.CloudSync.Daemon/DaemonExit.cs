namespace AStarDev.CloudSync.Daemon;

/// <summary>Holds the exit code the daemon process ends with: zero, or <see cref="ApplicationConstants.RestartExitCode"/> after a restart request, which makes the systemd user unit start it again.</summary>
internal sealed class DaemonExit
{
    /// <summary>The exit code <c>Program</c> returns once the host has stopped. Zero until a stop is requested.</summary>
    public int Code { get; set; }
}
