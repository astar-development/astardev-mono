namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>The in-app notification list: keeps every notification it is given, oldest first, so a view can list it.</summary>
internal sealed class InAppNotificationPresenter : INotificationPresenter
{
    private readonly List<Notification> presented = [];

    /// <summary>Everything presented so far, oldest first.</summary>
    public IReadOnlyList<Notification> Presented => presented;

    /// <inheritdoc/>
    public void Present(Notification notification) => presented.Add(notification);
}
