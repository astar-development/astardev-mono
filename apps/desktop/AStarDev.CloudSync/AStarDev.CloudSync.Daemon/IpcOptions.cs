namespace AStarDev.CloudSync.Daemon;

internal sealed class IpcOptions
{
    public string SocketPath { get; set; } = DefaultSocketPath(Environment.GetEnvironmentVariable(ApplicationConstants.XdgRuntimeDirEnvironmentVariable));

    // shortcut: no XDG_RUNTIME_DIR falls back to a per-user temp dir, tighten permissions when the IPC server binds it (S3).
    public static string DefaultSocketPath(string? xdgRuntimeDir)
        => Path.Combine(
            string.IsNullOrWhiteSpace(xdgRuntimeDir) ? Path.Combine(Path.GetTempPath(), Environment.UserName) : xdgRuntimeDir,
            ApplicationConstants.SocketDirectoryName,
            ApplicationConstants.SocketFileName);
}
