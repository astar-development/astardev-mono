namespace AStarDev.CloudSync.Daemon.Startup;

public sealed class StartupFailedException(string stageName, Exception inner)
    : Exception($"Startup stage '{stageName}' failed.", inner)
{
    public string StageName { get; } = stageName;
}
