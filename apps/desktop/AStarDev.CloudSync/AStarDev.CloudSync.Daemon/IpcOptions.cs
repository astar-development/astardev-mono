namespace AStarDev.CloudSync.Daemon;

public sealed class IpcOptions
{
    public const string SectionName = "Ipc";

    public string SocketPath { get; set; } = DefaultSocketPath(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"));

    // shortcut: no XDG_RUNTIME_DIR falls back to a per-user temp dir, tighten permissions when the IPC server binds it (S3).
    public static string DefaultSocketPath(string? xdgRuntimeDir)
        => Path.Combine(
            string.IsNullOrWhiteSpace(xdgRuntimeDir) ? Path.Combine(Path.GetTempPath(), Environment.UserName) : xdgRuntimeDir,
            "astardev-cloudsync",
            "daemon.sock");
}
