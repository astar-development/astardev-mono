using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ProvidersViewModelShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
    public async Task show_an_actionable_error_with_a_correlation_placeholder_and_a_diagnostics_link()
    {
        using var host = ProviderTestHost.Create(new FakeIpcClient([], available: false));

        await host.Accounts.RefreshAsync(Token);

        var error = host.Providers.Error.ShouldNotBeNull();
        error.Message.ShouldContain("daemon");
        error.RetryGuidance.ShouldNotStartWith("!");
        error.CorrelationText.ShouldNotStartWith("!");
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
