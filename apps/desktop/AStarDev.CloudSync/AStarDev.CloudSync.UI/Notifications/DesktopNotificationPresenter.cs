using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Presents each notification in the in-app list and through the operating system. A notifier that is missing or refuses leaves the in-app list as the only record. Clicking the operating system notification opens its target.</summary>
/// <param name="inner">Keeps the in-app list.</param>
/// <param name="notifier">Shows the operating system notification.</param>
/// <param name="lifetime">Abandons notifications still being shown when the application stops.</param>
/// <param name="service">Opens the target of a clicked notification. Resolved late because the service itself holds this presenter.</param>
/// <param name="post">Runs an action on the UI thread, where the window may be shown.</param>
internal sealed class DesktopNotificationPresenter(InAppNotificationPresenter inner, IDesktopNotifier notifier, IHostApplicationLifetime lifetime, Func<INotificationService> service, Action<Action> post) : INotificationPresenter
{
    /// <inheritdoc/>
    /// <remarks>Does not wait for the operating system: the notifier runs in the background. A failed notification is dropped; a clicked one opens its target on the UI thread.</remarks>
    public void Present(Notification notification)
    {
        inner.Present(notification);
        _ = ShowAsync(notification);
    }

    private async Task ShowAsync(Notification notification)
    {
        var shown = await notifier.ShowAsync(notification, lifetime.ApplicationStopping);
        if (shown.Value is Ok<bool> { Value: true }) post(() => service().Open(notification));
    }
}
