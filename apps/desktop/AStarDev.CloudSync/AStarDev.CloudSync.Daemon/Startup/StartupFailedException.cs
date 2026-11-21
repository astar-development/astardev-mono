namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Framework boundary only: thrown by <see cref="StartupHostedService"/> so the host fails to start.</summary>
internal sealed class StartupFailedException(StartupError error)
    : Exception($"Startup stage '{error.StageName}' failed: {error.Cause}", error.Exception)
{
    /// <summary>The failure that stopped startup, kept as a value so callers read the stage and cause without parsing <see cref="Exception.Message"/>.</summary>
    public StartupError Error { get; } = error;
}
