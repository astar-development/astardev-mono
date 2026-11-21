using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>How one <see cref="SyncHealth"/> looks on the Trust Status card: a headline and summary in words, plus an indicator giving colour and icon.</summary>
/// <param name="Headline">The card's main words, for example "YOUR DATA IS SAFE". Never blank.</param>
/// <param name="Summary">One sentence under the headline saying what, if anything, the user should do.</param>
/// <param name="Indicator">Colour, icon and short text for the state, so meaning never rests on colour alone.</param>
internal sealed record SyncHealthPresentation(string Headline, string Summary, StatusIndicatorViewModel Indicator)
{
    /// <summary>Builds the presentation of <paramref name="health"/> from the <c>Dashboard.Health.*</c> texts.</summary>
    /// <param name="health">The state to present.</param>
    /// <param name="localisation">Supplies the texts. A missing text shows as <c>!key!</c> rather than failing.</param>
    public static SyncHealthPresentation For(SyncHealth health, ILocalisationService localisation) => new(
        localisation.Get($"Dashboard.Health.{health}.Headline"),
        localisation.Get($"Dashboard.Health.{health}.Summary"),
        StatusIndicatorViewModel.For(LevelOf(health), localisation));

    /// <summary>The trust level, and so the colour and icon, that <paramref name="health"/> maps to. Each state maps to a different level.</summary>
    public static TrustLevel LevelOf(SyncHealth health) => health switch
    {
        SyncHealth.Healthy => TrustLevel.Healthy,
        SyncHealth.Active => TrustLevel.Active,
        SyncHealth.Degraded => TrustLevel.Degraded,
        SyncHealth.AttentionRequired => TrustLevel.Attention,
        _ => TrustLevel.Critical
    };
}
