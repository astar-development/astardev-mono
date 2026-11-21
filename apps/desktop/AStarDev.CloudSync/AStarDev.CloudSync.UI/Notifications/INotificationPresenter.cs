namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Shows a notification to the user. The seam where OS-level notifications plug in later.</summary>
internal interface INotificationPresenter
{
    /// <summary>Shows <paramref name="notification"/>. Called once per aggregated event, not once per repeat.</summary>
    void Present(Notification notification);
}
