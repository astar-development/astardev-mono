namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class ProvidersMessagesShould
{
    [Fact]
    public void round_trip_a_providers_view()
    {
        var view = new ProvidersView(
            [new ProviderView("OneDrive", [ProviderCapability.SelectiveSync, ProviderCapability.DeltaSync])],
            [new ProviderInsightsView(Guid.NewGuid(), DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), 40, 100, 1, 2, 3), new ProviderInsightsView(Guid.NewGuid(), null, null, null, 0, 0, 0)]);

        var element = IpcJson.ToElement(view).ShouldSucceed();

        IpcJson.FromElement<ProvidersView>(element).ShouldSucceed().Accounts.ShouldBe(view.Accounts);
        IpcJson.FromElement<ProvidersView>(element).ShouldSucceed().Providers.Single().Capabilities.ShouldBe([ProviderCapability.SelectiveSync, ProviderCapability.DeltaSync]);
    }
}
