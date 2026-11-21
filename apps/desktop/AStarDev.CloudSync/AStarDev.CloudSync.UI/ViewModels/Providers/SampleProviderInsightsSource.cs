using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>Placeholder figures, each worded as sample data, identical for every account.</summary>
/// <param name="localisation">Supplies the sample texts.</param>
internal sealed class SampleProviderInsightsSource(ILocalisationService localisation) : IProviderInsightsSource
{
    /// <inheritdoc/>
    public ProviderInsights Describe(AccountView account) => new(ProviderHealth.Healthy, localisation.Get("Providers.Card.LastSync.Sample"), localisation.Get("Providers.Card.Storage.Sample"), localisation.Get("Providers.Card.Activity.Sample"));
}
