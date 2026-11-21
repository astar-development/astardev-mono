namespace AStarDev.CloudSync.UI;

/// <summary>Where the daemon's socket lives. Mirrors the daemon's default so both sides meet without configuration.</summary>
internal static class DaemonSocketPath
{
    public static string Resolve(string? xdgRuntimeDir)
        => Path.Combine(
            string.IsNullOrWhiteSpace(xdgRuntimeDir) ? Path.Combine(Path.GetTempPath(), Environment.UserName) : xdgRuntimeDir,
            ApplicationConstants.SocketDirectoryName,
            ApplicationConstants.SocketFileName);
}
