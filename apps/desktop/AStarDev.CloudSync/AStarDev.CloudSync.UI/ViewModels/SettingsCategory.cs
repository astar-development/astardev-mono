namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>The sections of the Settings screen, in display order (doc 20).</summary>
internal enum SettingsCategory
{
    /// <summary>Settings that affect the whole application.</summary>
    General,

    /// <summary>Entry point to provider management.</summary>
    Providers,

    /// <summary>Sync folders and the local sync folder.</summary>
    Synchronisation,

    /// <summary>Theme and display.</summary>
    Appearance,

    /// <summary>Notification preferences.</summary>
    Notifications,

    /// <summary>Metrics, Logs, Traces and Diagnostics.</summary>
    Advanced
}
