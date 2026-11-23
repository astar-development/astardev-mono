namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>One instrument as text, ready to show as it is.</summary>
/// <param name="Name">The instrument name.</param>
/// <param name="Current">The latest value with its unit.</param>
/// <param name="History">The recent values, oldest first, separated by spaces.</param>
internal sealed record MetricRowViewModel(string Name, string Current, string History);
