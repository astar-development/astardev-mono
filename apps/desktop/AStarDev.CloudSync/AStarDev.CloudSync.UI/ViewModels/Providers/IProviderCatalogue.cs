namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>The provider metadata registry the Providers screen reads.</summary>
internal interface IProviderCatalogue
{
    /// <summary>Every provider offered in the add journey, available or not, in display order.</summary>
    IReadOnlyList<ProviderDescriptor> All { get; }

    /// <summary>The descriptor for <paramref name="providerType"/>. An unknown type gets an available descriptor named after the type with no capabilities, so its accounts still show.</summary>
    /// <param name="providerType">The provider identifier reported by the daemon.</param>
    ProviderDescriptor Find(string providerType);
}
