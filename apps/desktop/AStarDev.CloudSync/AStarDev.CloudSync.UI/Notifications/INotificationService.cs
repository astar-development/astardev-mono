using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Turns daemon events into notifications with fatigue control: routine success is quiet and repeats aggregate.</summary>
internal interface INotificationService
{
    /// <summary>The notifications still tracked for aggregation, oldest first.</summary>
    IReadOnlyList<Notification> Active { get; }

    /// <summary>What the user wants to hear about. <see cref="NotificationPreferences.Default"/> until the saved settings are applied; assigning takes effect for the next event.</summary>
    NotificationPreferences Preferences { get; set; }

    /// <summary>
    /// Handles <paramref name="notificationEvent"/>. An event the <see cref="Preferences"/> switch off does nothing, and so does a routine success unless completion is switched on. A repeat of an event still inside the aggregation window raises the count of
    /// <see cref="Active"/> without presenting again; anything else is presented.
    /// </summary>
    /// <param name="notificationEvent">What happened.</param>
    void Raise(NotificationEvent notificationEvent);

    /// <summary>Shows the window and navigates to the notification's target.</summary>
    /// <param name="notification">The notification the user activated.</param>
    /// <returns>The navigation result. The window is shown even when navigation fails.</returns>
    Result<Unit, NavigationError> Open(Notification notification);
}
