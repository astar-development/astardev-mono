namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Why a startup stage failed. <paramref name="Exception"/> is set when the cause was a captured exception.</summary>
internal sealed record StartupError(string StageName, string Cause, Exception? Exception = null);
