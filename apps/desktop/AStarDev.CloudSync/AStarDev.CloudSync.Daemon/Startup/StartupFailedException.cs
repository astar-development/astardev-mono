namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Framework boundary only: thrown by <see cref="StartupHostedService"/> so the host fails to start.</summary>
internal sealed class StartupFailedException(StartupError error)
    : Exception($"Startup stage '{error.StageName}' failed: {error.Cause}", error.Exception)
{
    public StartupError Error { get; } = error;
}
