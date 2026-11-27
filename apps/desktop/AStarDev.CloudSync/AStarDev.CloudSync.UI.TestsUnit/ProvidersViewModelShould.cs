using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Providers;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ProvidersViewModelShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Synced = new(2026, 11, 20, 12, 30, 0, TimeSpan.Zero);

    private static ProvidersViewModel ResolveFromComposition(FakeIpcClient client, out IHost host)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();
        builder.Services.AddSingleton<Func<IIpcClient>>(() => client);
        builder.Services.AddSingleton<IUriLauncher>(new FakeUriLauncher());
        host = builder.Build();

        return host.Services.GetServices<IScreen>().OfType<ProvidersViewModel>().Single();
    }

    [Fact]
    public void list_the_catalogue_from_the_daemon()
    {
        var client = new FakeIpcClient([], available: true)
        {
            ProvidersReply = new Ok<ProvidersView>(new ProvidersView([new ProviderView("OneDrive", [ProviderCapability.SelectiveSync]), new ProviderView("Other", [ProviderCapability.DeltaSync, ProviderCapability.Versioning])], []))
        };
        var screen = ResolveFromComposition(client, out var host);
        using var _ = host;

        screen.Add.Providers.ShouldBeEmpty();
        screen.OnNavigatedTo(null);

        screen.Add.Providers.Select(provider => provider.ProviderType).ShouldBe(["OneDrive", "Other"]);
        screen.Add.Providers[1].Capabilities.ShouldBe([ProviderCapability.DeltaSync, ProviderCapability.Versioning]);
        client.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task show_each_accounts_own_insights()
    {
        var ada = ProviderTestHost.Account("ada@example.com");
        var grace = ProviderTestHost.Account("grace@example.com");
        var client = ProviderTestHost.ClientWith(ada, grace);
        client.ProvidersReply = new Ok<ProvidersView>(new ProvidersView(
            [new ProviderView("OneDrive", [ProviderCapability.SelectiveSync])],
            [new ProviderInsightsView(ada.Id, Synced, 40_000_000_000, 100_000_000_000, 2, 1, 0, 7), new ProviderInsightsView(grace.Id, null, null, null, 0, 0, 3)]));
        var screen = ResolveFromComposition(client, out var host);
        using var _ = host;

        await screen.Accounts.RefreshAsync(Token);

        var cards = screen.Cards;
        cards.Select(card => card.AccountName).ShouldBe(["ada@example.com", "grace@example.com"]);
        cards[0].LastSyncText.ShouldStartWith("Last sync: ");
        cards[0].LastSyncText.ShouldNotContain("never");
        cards[1].LastSyncText.ShouldBe("Last sync: never");
        cards[0].StorageText.ShouldBe("Storage: 40 GB of 100 GB used");
        cards[1].StorageText.ShouldBe("Storage: unavailable");
        cards[0].RecentActivityText.ShouldBe("Recent activity: 2 waiting, 1 running, 0 failed, 7 synced");
        cards[1].RecentActivityText.ShouldBe("Recent activity: 0 waiting, 0 running, 3 failed, 0 synced");
        cards.Select(card => card.Health.Health).ShouldBe([ProviderHealth.Syncing, ProviderHealth.Warning]);
        cards[0].Capabilities.ShouldBe([ProviderCapability.SelectiveSync]);
    }

    [Fact]
    public async Task keep_the_earlier_figures_when_the_daemon_cannot_be_asked()
    {
        var ada = ProviderTestHost.Account("ada@example.com");
        var client = ProviderTestHost.ClientWith(ada);
        client.ProvidersReply = new Ok<ProvidersView>(new ProvidersView([], [new ProviderInsightsView(ada.Id, null, null, null, 4, 0, 0)]));
        var screen = ResolveFromComposition(client, out var host);
        using var _ = host;
        await screen.Accounts.RefreshAsync(Token);

        client.ProvidersReply = new Fail<IpcError>(new HandlerFailed("no"));
        screen.OnNavigatedTo(null);

        screen.Cards.Single().RecentActivityText.ShouldBe("Recent activity: 4 waiting, 0 running, 0 failed, 0 synced");
    }

    [Fact]
    public void show_every_health_state()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith());

        var states = host.Providers.HealthSummary.Select(item => item.Health).ToList();

        states.Select(state => state.Health).ShouldBe(Enum.GetValues<ProviderHealth>());
        states.Select(state => state.Text).Distinct().Count().ShouldBe(6);
        states.Select(state => state.IconKey).Distinct().Count().ShouldBe(6);
        states.Select(state => state.ColourKey).Distinct().Count().ShouldBe(6);
        foreach (var state in states) state.Text.ShouldNotStartWith("!");
    }

    [Fact]
    public async Task list_multiple_accounts_independently()
    {
        var ada = ProviderTestHost.Account("ada@example.com");
        var grace = ProviderTestHost.Account("grace@example.com", state: AccountState.ReauthenticationRequired);
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(ada, grace));

        await host.Accounts.RefreshAsync(Token);

        host.Providers.Cards.Select(card => card.AccountName).ShouldBe(["ada@example.com", "grace@example.com"]);
        host.Providers.Cards.Select(card => card.Health.Health).ShouldBe([ProviderHealth.Healthy, ProviderHealth.AuthenticationRequired]);
        host.Providers.Cards.Select(card => card.CanReconnect).ShouldBe([false, true]);
        host.Providers.HealthSummary.Single(item => item.Health.Health == ProviderHealth.Healthy).Count.ShouldBe(1);
        host.Providers.HealthSummary.Single(item => item.Health.Health == ProviderHealth.AuthenticationRequired).Count.ShouldBe(1);
        host.Providers.HealthSummary.Single(item => item.Health.Health == ProviderHealth.Error).Count.ShouldBe(0);
    }

    [Fact]
    public async Task keep_a_non_blocking_renewal_warning_while_an_account_needs_signing_in()
    {
        var grace = ProviderTestHost.Account("grace@example.com", state: AccountState.ReauthenticationRequired);
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(ProviderTestHost.Account("ada@example.com"), grace));

        host.Providers.HasRenewalWarning.ShouldBeFalse();
        await host.Accounts.RefreshAsync(Token);

        host.Providers.HasRenewalWarning.ShouldBeTrue();
        host.Providers.RenewalWarningText.ShouldContain("grace@example.com");
        host.Providers.RenewalWarningText.ShouldNotContain("ada@example.com");
        host.Providers.Cards.Count.ShouldBe(2);
    }

    [Fact]
    public async Task renew_sign_in_from_the_warning()
    {
        var grace = ProviderTestHost.Account("grace@example.com", state: AccountState.ReauthenticationRequired);
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith(grace));
        await host.Accounts.RefreshAsync(Token);

        await host.Providers.RenewSignInCommand.ExecuteAsync(null);

        host.Client.Sent.ShouldHaveSingleItem().ShouldBeOfType<ConnectAccount>();
    }

    [Fact]
    public async Task show_an_actionable_error_with_no_trace_and_a_diagnostics_link()
    {
        using var host = ProviderTestHost.Create(new FakeIpcClient([], available: false));

        await host.Accounts.RefreshAsync(Token);

        var error = host.Providers.Error.ShouldNotBeNull();
        error.Message.ShouldContain("daemon");
        error.RetryGuidance.ShouldNotStartWith("!");
        error.CorrelationText.ShouldNotStartWith("!");
        error.CorrelationText.ShouldContain("none");
        error.OpenDiagnosticsCommand.Execute(null);
        host.Navigation.Current.Id.ShouldBe(ScreenId.Diagnostics);
    }

    [Fact]
    public async Task show_no_error_after_a_request_that_works()
    {
        using var host = ProviderTestHost.Create(ProviderTestHost.ClientWith());
        await host.Accounts.RefreshAsync(Token);

        host.Providers.Error.ShouldBeNull();
        host.Providers.HasError.ShouldBeFalse();
    }

    [Fact]
    public void be_registered_as_the_providers_screen_from_the_real_composition()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();
        using var host = builder.Build();

        var screen = host.Services.GetServices<IScreen>().Single(candidate => candidate.Id == ScreenId.Providers);

        screen.Title.ShouldBe("Providers");
        screen.GetType().Name.ShouldBe("ProvidersViewModel");
    }
}
