namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Stands in for OS notifications: keeps what it is given so a view can list it.</summary>
internal sealed class PlaceholderNotificationPresenter : INotificationPresenter
{
    private readonly List<Notification> presented = [];

    /// <summary>Everything presented so far, oldest first.</summary>
    public IReadOnlyList<Notification> Presented => presented;

    /// <inheritdoc/>
    public void Present(Notification notification) => presented.Add(notification);
}
