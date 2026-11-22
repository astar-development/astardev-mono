namespace AStarDev.CloudSync.UI;

/// <summary>Where the daemon's socket lives. Mirrors the daemon's default so both sides meet without configuration.</summary>
internal static class DaemonSocketPath
{
    public static string Resolve(string? xdgRuntimeDir)
        => Path.Combine(RuntimeFolder(xdgRuntimeDir), ApplicationConstants.SocketDirectoryName, ApplicationConstants.SocketFileName);

    public static string InstanceFolder(string? xdgRuntimeDir)
        => Path.Combine(RuntimeFolder(xdgRuntimeDir), ApplicationConstants.InstanceDirectoryName);

    private static string RuntimeFolder(string? xdgRuntimeDir)
        => string.IsNullOrWhiteSpace(xdgRuntimeDir) ? Path.Combine(Path.GetTempPath(), Environment.UserName) : xdgRuntimeDir;
}
