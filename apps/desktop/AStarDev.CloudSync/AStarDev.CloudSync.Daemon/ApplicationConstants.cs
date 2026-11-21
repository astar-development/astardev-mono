namespace AStarDev.CloudSync.Daemon;

/// <summary>Project-specific strings for the daemon, so none are repeated as literals.</summary>
internal static class ApplicationConstants
{
    public static string ServiceName => "astardev-cloudsync-daemon";

    public static string IpcConfigurationSection => "Ipc";

    public static string XdgRuntimeDirEnvironmentVariable => "XDG_RUNTIME_DIR";

    public static string SocketDirectoryName => "astardev-cloudsync";

    public static string SocketFileName => "daemon.sock";

    public static string UnknownVersion => "unknown";

    public static string ProcessStartStageName => "Process start";

    public static string InfrastructureInitialisationStageName => "Infrastructure initialisation";

    public static string StateRecoveryStageName => "State recovery";

    public static string QueueRecoveryStageName => "Queue recovery";

    public static string BackgroundServicesStageName => "Background services";

    public static string AcceptConnectionsStageName => "Accept connections";
}
