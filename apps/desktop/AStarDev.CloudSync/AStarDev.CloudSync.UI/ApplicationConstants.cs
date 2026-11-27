namespace AStarDev.CloudSync.UI;

/// <summary>Project-specific strings for the UI, so none are repeated as literals.</summary>
internal static class ApplicationConstants
{
    public static string ServiceName => "astardev-cloudsync-ui";

    public static string XdgRuntimeDirEnvironmentVariable => "XDG_RUNTIME_DIR";

    public static string SocketDirectoryName => "astardev-cloudsync";

    public static string SocketFileName => "daemon.sock";

    public static string InstanceDirectoryName => "astardev-cloudsync-ui";

    public static string InstanceName => "ui";

    public static string ConnectedText => "Connected";

    public static string ReconnectingText => "Reconnecting";

    public static string DisconnectedText => "Disconnected";

    public static string DaemonUnreachableText => "The CloudSync daemon is not reachable. Start the daemon and try again.";

    public static string UnexpectedReplyText => "The daemon sent a reply the UI could not read.";

    public static string ConnectRejectedText => "The daemon declined to connect the account.";

    public static string OneItemText => "1 item in the drive root";

    public static string ItemCountFormat => "{0} items in the drive root";

    public static string RequestRejectedText => "The daemon declined the request.";

    public static string AccountConnectedText => "Connected";

    public static string ReauthenticationRequiredText => "Sign in again to resume syncing";

    public static string LoadingFoldersText => "Loading...";

    public static string MoreFoldersText => "Show more folders";

    public static string ConnectAccountFirstText => "Connect an account first.";

    public static string JobNotFoundText => "The sync job was created but the daemon did not list it. Restart the app and try again.";

    public static string JobSummaryFormat => "Syncing {0} ({1} mode)";

    public static string PickFolderTitle => "Choose the local sync folder";

    public static string SaveFileTitle => "Save the export";

    public static string CsvExtension => "csv";
}
