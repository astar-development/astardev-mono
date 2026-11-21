using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;

namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Localised placeholder notifications, one per category, until the daemon raises real events.</summary>
internal static class SampleNotifications
{
    /// <summary>One sample for each <see cref="NotificationCategory"/>, in enum order.</summary>
    /// <param name="localisation">Supplies the sample texts.</param>
    public static IReadOnlyList<Notification> Create(ILocalisationService localisation) =>
    [
        Sample(localisation, NotificationCategory.Informational, ScreenId.Providers),
        Sample(localisation, NotificationCategory.Progress, ScreenId.Activity),
        Sample(localisation, NotificationCategory.Warning, ScreenId.Providers),
        Sample(localisation, NotificationCategory.Conflict, ScreenId.Conflicts),
        Sample(localisation, NotificationCategory.Error, ScreenId.Activity),
        Sample(localisation, NotificationCategory.Critical, ScreenId.Dashboard)
    ];

    private static Notification Sample(ILocalisationService localisation, NotificationCategory category, ScreenId screen) =>
        new(category, $"Sample:{category}", localisation.Get($"Notifications.Sample.{category}.Title"), localisation.Get($"Notifications.Sample.{category}.Message"), localisation.Get($"Notifications.Sample.{category}.NextStep"), new NotificationTarget(screen, null), 1);
}
