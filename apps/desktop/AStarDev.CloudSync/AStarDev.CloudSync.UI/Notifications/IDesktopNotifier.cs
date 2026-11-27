using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Shows a notification through the operating system, so it reaches the user while the main window is hidden in the tray.</summary>
internal interface IDesktopNotifier
{
    /// <summary>Asks the operating system to show <paramref name="notification"/>.</summary>
    /// <param name="notification">What to show.</param>
    /// <param name="cancellationToken">Cancelling abandons the request; it surfaces as <see cref="OperationCanceledException"/>, not as a failure.</param>
    /// <returns><see langword="true"/> when the user clicked the notification, <see langword="false"/> when it closed without a click. Completes only once the notification has closed. Otherwise <see cref="NotifierUnavailable"/> when this platform has no way to show it, or <see cref="NotifierFailed"/> when it refused.</returns>
    Task<Result<bool, DesktopNotifierError>> ShowAsync(Notification notification, CancellationToken cancellationToken);
}
