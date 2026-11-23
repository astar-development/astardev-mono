using AStarDev.CloudSync.IPC;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>The providers the daemon reports, each available, with the capabilities of the provider the daemon has registered.</summary>
/// <param name="source">Holds what the daemon reported at the last refresh.</param>
internal sealed class IpcProviderCatalogue(IpcProvidersSource source) : IProviderCatalogue
{
    /// <inheritdoc/>
    /// <remarks>Empty until the first <see cref="IProviderInsightsSource.RefreshAsync"/> succeeds, and after that as the daemon last reported.</remarks>
    public IReadOnlyList<ProviderDescriptor> All => [.. source.Current.Providers.Select(Describe)];

    /// <inheritdoc/>
    public ProviderDescriptor Find(string providerType) => All.Where(descriptor => descriptor.ProviderType == providerType).Concat([new ProviderDescriptor(providerType, providerType, true, [])]).First();

    private static ProviderDescriptor Describe(ProviderView provider) => new(provider.ProviderType, provider.ProviderType, true, provider.Capabilities);
}
