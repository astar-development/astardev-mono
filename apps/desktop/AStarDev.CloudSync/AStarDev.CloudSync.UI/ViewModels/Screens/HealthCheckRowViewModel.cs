namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>One health check as text, ready to show as it is.</summary>
/// <param name="Name">The name the check is registered under in the daemon.</param>
/// <param name="Status">The localised rating of the check.</param>
internal sealed record HealthCheckRowViewModel(string Name, string Status);
