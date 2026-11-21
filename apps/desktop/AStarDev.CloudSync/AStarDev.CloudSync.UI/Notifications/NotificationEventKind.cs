namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>What the daemon reported, before the notification service decides whether and how to tell the user.</summary>
internal enum NotificationEventKind
{
    /// <summary>A routine sync finished successfully. Never notified.</summary>
    SyncSucceeded,

    /// <summary>A provider account was connected.</summary>
    ProviderConnected,

    /// <summary>A long-running operation made progress.</summary>
    SyncProgress,

    /// <summary>Provider credentials are about to expire.</summary>
    AuthenticationExpiring,

    /// <summary>Two versions of an item disagree.</summary>
    ConflictDetected,

    /// <summary>An item failed to sync.</summary>
    ItemFailed,

    /// <summary>Data may be at risk or sync has stopped.</summary>
    DataAtRisk
}
