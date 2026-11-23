using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Presents each notification in the in-app list and through the operating system. A notifier that is missing or refuses leaves the in-app list as the only record.</summary>
/// <param name="inner">Keeps the in-app list.</param>
/// <param name="notifier">Shows the operating system notification.</param>
/// <param name="lifetime">Abandons notifications still being shown when the application stops.</param>
internal sealed class DesktopNotificationPresenter(InAppNotificationPresenter inner, IDesktopNotifier notifier, IHostApplicationLifetime lifetime) : INotificationPresenter
{
    /// <inheritdoc/>
    /// <remarks>Does not wait for the operating system: the notifier runs in the background and its outcome is dropped.</remarks>
    public void Present(Notification notification)
    {
        inner.Present(notification);
        _ = notifier.ShowAsync(notification, lifetime.ApplicationStopping);
    }
}
