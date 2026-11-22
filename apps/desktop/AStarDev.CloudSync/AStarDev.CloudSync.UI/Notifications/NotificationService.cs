using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.Tray;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>The default <see cref="INotificationService"/>.</summary>
/// <param name="presenter">Shows what is not suppressed.</param>
/// <param name="tray">Routes deep links.</param>
/// <param name="localisation">Supplies every text.</param>
/// <param name="clock">Measures the aggregation window.</param>
/// <param name="window">How long a repeat of a presented event is folded into it.</param>
internal sealed class NotificationService(INotificationPresenter presenter, TrayLifecycle tray, ILocalisationService localisation, TimeProvider clock, TimeSpan window) : INotificationService
{
    private readonly Dictionary<string, (Notification Notification, DateTimeOffset At)> active = [];

    /// <inheritdoc/>
    public IReadOnlyList<Notification> Active => active.Values.OrderBy(entry => entry.At).Select(entry => entry.Notification).ToList();

    /// <inheritdoc/>
    public void Raise(NotificationEvent notificationEvent)
    {
        if (notificationEvent.Kind == NotificationEventKind.SyncSucceeded) return;

        var key = $"{notificationEvent.Kind}:{notificationEvent.SubjectId}";
        var now = clock.GetUtcNow();
        if (active.TryGetValue(key, out var existing) && now - existing.At <= window)
        {
            active[key] = (Build(notificationEvent, key, existing.Notification.Count + 1), existing.At);

            return;
        }

        var notification = Build(notificationEvent, key, 1);
        active[key] = (notification, now);
        presenter.Present(notification);
    }

    /// <inheritdoc/>
    public Result<Unit, NavigationError> Open(Notification notification) => tray.Open(notification.Target.Screen, notification.Target.TargetId);

    private Notification Build(NotificationEvent notificationEvent, string key, int count)
    {
        var kind = notificationEvent.Kind;
        var message = localisation.Get($"Notifications.{kind}.Message");
        var shown = count > 1 ? localisation.Format("Notifications.Repeated", ("message", message), ("count", count)) : message;

        return new Notification(CategoryOf(kind), key, localisation.Get($"Notifications.{kind}.Title"), shown, localisation.Get($"Notifications.{kind}.NextStep"), TargetOf(notificationEvent), count);
    }

    private static NotificationCategory CategoryOf(NotificationEventKind kind) => kind switch
    {
        NotificationEventKind.SyncSucceeded => NotificationCategory.Informational,
        NotificationEventKind.ProviderConnected => NotificationCategory.Informational,
        NotificationEventKind.SyncProgress => NotificationCategory.Progress,
        NotificationEventKind.AuthenticationExpiring => NotificationCategory.Warning,
        NotificationEventKind.ConflictDetected => NotificationCategory.Conflict,
        NotificationEventKind.ItemFailed => NotificationCategory.Error,
        NotificationEventKind.AuthenticationExpired => NotificationCategory.Error,
        NotificationEventKind.InitialCrawlComplete => NotificationCategory.Informational,
        NotificationEventKind.SyncScopeComplete => NotificationCategory.Informational,
        NotificationEventKind.UserActionRequired => NotificationCategory.Warning,
        _ => NotificationCategory.Critical
    };

    private static NotificationTarget TargetOf(NotificationEvent notificationEvent) => notificationEvent.Kind switch
    {
        NotificationEventKind.SyncSucceeded => new NotificationTarget(ScreenId.Activity, notificationEvent.SubjectId),
        NotificationEventKind.ProviderConnected => new NotificationTarget(ScreenId.Providers, notificationEvent.SubjectId),
        NotificationEventKind.SyncProgress => new NotificationTarget(ScreenId.Activity, notificationEvent.SubjectId),
        NotificationEventKind.AuthenticationExpiring => new NotificationTarget(ScreenId.Providers, notificationEvent.SubjectId),
        NotificationEventKind.ConflictDetected => new NotificationTarget(ScreenId.Conflicts, notificationEvent.SubjectId),
        NotificationEventKind.ItemFailed => new NotificationTarget(ScreenId.Activity, notificationEvent.SubjectId),
        NotificationEventKind.AuthenticationExpired => new NotificationTarget(ScreenId.Providers, notificationEvent.SubjectId),
        NotificationEventKind.InitialCrawlComplete => new NotificationTarget(ScreenId.Activity, notificationEvent.SubjectId),
        NotificationEventKind.SyncScopeComplete => new NotificationTarget(ScreenId.Activity, null),
        NotificationEventKind.UserActionRequired => new NotificationTarget(ScreenId.Activity, notificationEvent.SubjectId),
        _ => new NotificationTarget(ScreenId.Dashboard, null)
    };
}
