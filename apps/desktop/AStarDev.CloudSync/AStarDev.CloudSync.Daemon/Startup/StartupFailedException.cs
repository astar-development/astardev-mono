using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Framework boundary only: thrown by <see cref="StartupHostedService"/> so the host fails to start.</summary>
internal sealed class StartupFailedException : Exception
{
    /// <summary>Wraps <paramref name="error"/> and sets <see cref="Exception.HelpLink"/> to the link of its code.</summary>
    public StartupFailedException(StartupError error)
        : base($"Startup stage '{error.StageName}' failed: {error.Cause}", error.Exception)
    {
        Error = error;
        HelpLink = error.HelpLink;
    }

    /// <summary>The failure that stopped startup, kept as a value so callers read the stage and cause without parsing <see cref="Exception.Message"/>.</summary>
    public StartupError Error { get; }
}
