using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.ViewModels;

namespace AStarDev.CloudSync.UI.Tray;

/// <summary>What the tray icon conveys for one trust level: a distinct icon and tooltip text, so meaning never rests on colour alone.</summary>
/// <param name="Level">The trust level shown.</param>
/// <param name="IconKey">The theme resource key of the matching icon geometry, the same one <see cref="StatusIndicatorViewModel"/> uses.</param>
/// <param name="ToolTip">The localised tooltip. Different for every level and never blank.</param>
internal sealed record TrayState(TrustLevel Level, string IconKey, string ToolTip)
{
    /// <summary>The tray state for <paramref name="level"/>.</summary>
    /// <param name="level">The level to convey.</param>
    /// <param name="localisation">Supplies the tooltip text.</param>
    public static TrayState For(TrustLevel level, ILocalisationService localisation) => new(level, $"Icon.Trust.{level}", localisation.Get($"Tray.State.{level}"));
}
