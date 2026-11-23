namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Which events the user wants to be notified about. An event kind with no switch here is always notified.</summary>
/// <param name="Conflicts">A new conflict is notified.</param>
/// <param name="Errors">A failed item or an expired sign-in is notified.</param>
/// <param name="Completion">A finished sync is notified; otherwise routine success stays quiet.</param>
internal sealed record NotificationPreferences(bool Conflicts, bool Errors, bool Completion)
{
    /// <summary>Conflicts and errors notify, completion does not; matches the daemon's defaults for the notification settings.</summary>
    public static NotificationPreferences Default { get; } = new(true, true, false);
}
