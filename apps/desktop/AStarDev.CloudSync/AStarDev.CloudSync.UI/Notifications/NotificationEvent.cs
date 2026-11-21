namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>One thing that happened.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="SubjectId">The provider, conflict or activity record it concerns. Events of the same kind and subject aggregate; it is also the deep link target id.</param>
internal sealed record NotificationEvent(NotificationEventKind Kind, string SubjectId);
