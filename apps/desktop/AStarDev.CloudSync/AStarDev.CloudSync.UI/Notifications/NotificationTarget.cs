using AStarDev.CloudSync.UI.Navigation;

namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>Where a notification leads.</summary>
/// <param name="Screen">The destination screen.</param>
/// <param name="TargetId">The provider, conflict or activity record to focus, or <see langword="null"/> for the screen itself.</param>
internal sealed record NotificationTarget(ScreenId Screen, string? TargetId);
