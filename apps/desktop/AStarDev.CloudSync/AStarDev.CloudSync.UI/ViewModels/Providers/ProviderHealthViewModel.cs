using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>One <see cref="ProviderHealth"/> as colour, icon and text together. <c>ProviderHealthView</c> draws it.</summary>
/// <param name="health">The state shown.</param>
/// <param name="text">The words shown beside the icon.</param>
internal sealed class ProviderHealthViewModel(ProviderHealth health, string text) : ViewModelBase
{
    /// <summary>The state shown.</summary>
    public ProviderHealth Health => health;

    /// <summary>The words shown beside the icon.</summary>
    public string Text => text;

    /// <summary>The theme resource key of the brush, for example <c>Providers.Health.Error.Brush</c>.</summary>
    public string ColourKey => $"Providers.Health.{health}.Brush";

    /// <summary>The resource key of the icon geometry. Each state has its own shape, so colour is never the only signal.</summary>
    public string IconKey => $"Icon.Providers.Health.{health}";

    internal static ProviderHealthViewModel For(ProviderHealth health, ILocalisationService localisation) => new(health, localisation.Get($"Providers.Health.{health}"));
}
