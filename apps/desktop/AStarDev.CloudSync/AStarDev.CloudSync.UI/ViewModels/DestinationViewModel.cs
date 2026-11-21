using AStarDev.CloudSync.UI.Navigation;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>One navigation rail entry.</summary>
/// <param name="Id">The screen it opens.</param>
/// <param name="Title">Localised name, shown as the tooltip.</param>
/// <param name="Glyph">Localised symbol drawn on the rail.</param>
public sealed record DestinationViewModel(ScreenId Id, string Title, string Glyph);
