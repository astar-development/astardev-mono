namespace AStarDev.CloudSync.Daemon;

/// <summary>The <c>Ipc</c> configuration section: where the daemon creates its socket.</summary>
internal sealed class IpcOptions
{
    /// <summary>The daemon's Unix socket file. Set <c>Ipc:SocketPath</c> to override the <see cref="DefaultSocketPath"/> for the current <c>XDG_RUNTIME_DIR</c>.</summary>
    public string SocketPath { get; set; } = DefaultSocketPath(Environment.GetEnvironmentVariable(ApplicationConstants.XdgRuntimeDirEnvironmentVariable));

    // shortcut: no XDG_RUNTIME_DIR falls back to a per-user temp dir, tighten permissions when the IPC server binds it (S3).
    /// <summary>Builds <c>&lt;runtime dir&gt;/astardev-cloudsync/daemon.sock</c>, using <paramref name="xdgRuntimeDir"/> when set and a per-user folder under the temp directory when not.</summary>
    public static string DefaultSocketPath(string? xdgRuntimeDir)
        => Path.Combine(
            string.IsNullOrWhiteSpace(xdgRuntimeDir) ? Path.Combine(Path.GetTempPath(), Environment.UserName) : xdgRuntimeDir,
            ApplicationConstants.SocketDirectoryName,
            ApplicationConstants.SocketFileName);
}
