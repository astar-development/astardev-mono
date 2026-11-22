namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>This platform has no notifier, or it is not installed.</summary>
/// <param name="Reason">What is missing, for the log.</param>
internal sealed record NotifierUnavailable(string Reason);

/// <summary>The notifier ran and refused the notification.</summary>
/// <param name="Reason">Why, for the log.</param>
internal sealed record NotifierFailed(string Reason);

/// <summary>Everything that can go wrong showing a desktop notification, as values. Match it exhaustively.</summary>
internal union DesktopNotifierError(NotifierUnavailable, NotifierFailed);
