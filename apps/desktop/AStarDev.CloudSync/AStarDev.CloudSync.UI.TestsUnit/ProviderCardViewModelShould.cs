using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels.Providers;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ProviderCardViewModelShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeCatalogue(params ProviderDescriptor[] descriptors) : IProviderCatalogue
    {
        public IReadOnlyList<ProviderDescriptor> All => descriptors;

        public ProviderDescriptor Find(string providerType) => descriptors.FirstOrDefault(descriptor => descriptor.ProviderType == providerType) ?? new ProviderDescriptor(providerType, providerType, true, []);
    }

    [Fact]
    public async Task drive_actions_from_capabilities()
    {
        var full = new ProviderDescriptor("Full", "Full Drive", true, [ProviderCapability.SelectiveSync, ProviderCapability.DeltaSync, ProviderCapability.Versioning]);
        var bare = new ProviderDescriptor("Bare", "Bare Drive", true, []);
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(ProviderTestHost.Account("a", "Full"), ProviderTestHost.Account("b", "Bare"), ProviderTestHost.Account("c", "Unlisted")), new FakeCatalogue(full, bare));

        await host.Accounts.RefreshAsync(Token);

        var cards = host.Providers.Cards;
        cards[0].CanManage.ShouldBeTrue();
        cards[0].CanPause.ShouldBeTrue();
        cards[0].Capabilities.ShouldBe([ProviderCapability.SelectiveSync, ProviderCapability.DeltaSync, ProviderCapability.Versioning]);
        cards[1].CanManage.ShouldBeFalse();
        cards[1].CanPause.ShouldBeFalse();
        cards[2].CanManage.ShouldBeFalse();
        cards[2].ProviderName.ShouldBe("Unlisted");
        foreach (var card in cards) card.CanRemove.ShouldBeTrue();
    }

    [Fact]
    public async Task not_offer_pause_while_sign_in_is_expired()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(ProviderTestHost.Account("a", state: AccountState.ReauthenticationRequired)));

        await host.Accounts.RefreshAsync(Token);

        host.Providers.Cards[0].CanPause.ShouldBeFalse();
        host.Providers.Cards[0].CanReconnect.ShouldBeTrue();
    }

    [Fact]
    public async Task label_last_sync_storage_and_activity_as_sample_data()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(ProviderTestHost.Account("a")));

        await host.Accounts.RefreshAsync(Token);

        var card = host.Providers.Cards[0];
        card.LastSyncText.ShouldContain("sample");
        card.StorageText.ShouldContain("sample");
        card.RecentActivityText.ShouldContain("sample");
        card.ProviderName.ShouldBe("OneDrive");
    }

    [Fact]
    public async Task toggle_pause_without_changing_health()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(ProviderTestHost.Account("a")));
        await host.Accounts.RefreshAsync(Token);
        var card = host.Providers.Cards[0];

        card.PauseCommand.Execute(null);

        card.IsPaused.ShouldBeTrue();
        card.PauseLabel.ShouldBe("Resume");
        card.Health.Health.ShouldBe(ProviderHealth.Healthy);
    }

    [Fact]
    public async Task send_connect_when_reconnecting_from_the_card()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(ProviderTestHost.Account("a", state: AccountState.ReauthenticationRequired)));
        await host.Accounts.RefreshAsync(Token);

        await host.Providers.Cards[0].ReconnectCommand.ExecuteAsync(null);

        host.Client.Sent.ShouldHaveSingleItem().ShouldBeOfType<ConnectAccount>();
    }
}
