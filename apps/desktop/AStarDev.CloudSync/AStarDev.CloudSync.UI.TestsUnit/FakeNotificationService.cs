using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.Notifications;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class FakeNotificationService : INotificationService
{
    public IReadOnlyList<Notification> Active => [];

    public NotificationPreferences Preferences { get; set; } = NotificationPreferences.Default;

    public void Raise(NotificationEvent notificationEvent)
    {
    }

    public Result<Unit, NavigationError> Open(Notification notification) => new Ok<Unit>(default);
}
