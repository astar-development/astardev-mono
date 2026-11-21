using AStarDev.CloudSync.UI.Conflicts;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>One resolution button of the Conflicts screen.</summary>
/// <param name="Resolution">The resolution it requests.</param>
/// <param name="Label">Localised button text.</param>
/// <param name="IsEnabled">False while the daemon cannot perform the resolution; the screen explains why.</param>
internal sealed record ConflictActionViewModel(ConflictResolution Resolution, string Label, bool IsEnabled);
