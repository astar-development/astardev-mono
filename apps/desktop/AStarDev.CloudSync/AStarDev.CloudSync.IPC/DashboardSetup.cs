namespace AStarDev.CloudSync.IPC;

/// <summary>How far first-time setup has got, which decides whether the dashboard shows sync progress at all.</summary>
public enum DashboardSetup
{
    /// <summary>No account is connected, so there is nothing to sync.</summary>
    NoAccount,

    /// <summary>An account is connected but no folder is selected for sync in any job.</summary>
    NoFolders,

    /// <summary>An account is connected and at least one folder is selected, so sync runs or is about to.</summary>
    Ready
}
