using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels.Providers;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ProviderCardViewModelShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task drive_actions_from_capabilities()
    {
        var full = new ProviderDescriptor("Full", "Full Drive", [ProviderCapability.SelectiveSync, ProviderCapability.DeltaSync, ProviderCapability.Versioning]);
        var bare = new ProviderDescriptor("Bare", "Bare Drive", []);
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(ProviderTestHost.Account("a", "Full"), ProviderTestHost.Account("b", "Bare"), ProviderTestHost.Account("c", "Unlisted")), new FakeProviderCatalogue(full, bare));

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
