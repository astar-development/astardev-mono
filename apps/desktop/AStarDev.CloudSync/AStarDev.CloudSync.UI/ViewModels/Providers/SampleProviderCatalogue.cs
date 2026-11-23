using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>Placeholder catalogue: OneDrive is real, the other providers are listed as not yet available.</summary>
internal sealed class SampleProviderCatalogue : IProviderCatalogue
{
    /// <summary>Builds the catalogue with provider names from localisation.</summary>
    /// <param name="localisation">Supplies the display names.</param>
    public SampleProviderCatalogue(ILocalisationService localisation) => All =
    [
        new ProviderDescriptor("OneDrive", localisation.Get("Providers.Provider.OneDrive"), true, [ProviderCapability.SelectiveSync, ProviderCapability.DeltaSync, ProviderCapability.Versioning, ProviderCapability.RecycleBin, ProviderCapability.SharedContent]),
        new ProviderDescriptor("GoogleDrive", localisation.Get("Providers.Provider.GoogleDrive"), false, []),
        new ProviderDescriptor("Dropbox", localisation.Get("Providers.Provider.Dropbox"), false, [])
    ];

    /// <inheritdoc/>
    public IReadOnlyList<ProviderDescriptor> All { get; }

    /// <inheritdoc/>
    public ProviderDescriptor Find(string providerType) => All.FirstOrDefault(descriptor => descriptor.ProviderType == providerType) ?? new ProviderDescriptor(providerType, providerType, true, []);
}
