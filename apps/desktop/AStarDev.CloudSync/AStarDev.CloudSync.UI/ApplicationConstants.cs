namespace AStarDev.CloudSync.UI;

/// <summary>Project-specific strings for the UI, so none are repeated as literals.</summary>
internal static class ApplicationConstants
{
    public static string ServiceName => "astardev-cloudsync-ui";

    public static string XdgRuntimeDirEnvironmentVariable => "XDG_RUNTIME_DIR";

    public static string SocketDirectoryName => "astardev-cloudsync";

    public static string SocketFileName => "daemon.sock";

    public static string ConnectedText => "Connected";

    public static string ReconnectingText => "Reconnecting";

    public static string DisconnectedText => "Disconnected";
}
