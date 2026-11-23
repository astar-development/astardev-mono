using AStarDev.CloudSync.UI.ViewModels.Providers;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal sealed class FakeProviderCatalogue(params ProviderDescriptor[] descriptors) : IProviderCatalogue
{
    public IReadOnlyList<ProviderDescriptor> All => descriptors;

    public ProviderDescriptor Find(string providerType) => descriptors.FirstOrDefault(descriptor => descriptor.ProviderType == providerType) ?? new ProviderDescriptor(providerType, providerType, []);
}
