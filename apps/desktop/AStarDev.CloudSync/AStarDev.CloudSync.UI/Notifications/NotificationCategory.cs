namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>What a notification is about, as doc 23 names them. Decides how prominently a presenter shows it.</summary>
internal enum NotificationCategory
{
    /// <summary>Something happened that needs no action.</summary>
    Informational,

    /// <summary>A long-running operation is under way.</summary>
    Progress,

    /// <summary>Something may need action soon, such as expiring credentials.</summary>
    Warning,

    /// <summary>Two versions of an item disagree and the user must choose.</summary>
    Conflict,

    /// <summary>An operation failed and the user may need to recover.</summary>
    Error,

    /// <summary>Data may be at risk or sync has stopped.</summary>
    Critical
}
