using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class StateRecoveryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task reconnect_stored_accounts_silently()
    {
        var provider = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("acct-1", AccountStatus.Connected, Token);

        var result = await Stage(host).ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        provider.Reconnected.ShouldBe(["acct-1"]);
        (await StatusOf(host, account)).ShouldBe(AccountStatus.Connected);
    }

    [Fact]
    public async Task require_reauthentication_when_the_silent_token_fails()
    {
        var provider = new StubCloudProvider { ReconnectResult = new Fail<ProviderError>(new AuthenticationFailed("expired")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("acct-1", AccountStatus.Connected, Token);

        var result = await Stage(host).ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        (await StatusOf(host, account)).ShouldBe(AccountStatus.ReauthenticationRequired);
    }

    [Fact]
    public async Task connect_an_account_again_once_its_silent_token_works()
    {
        var provider = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("acct-1", AccountStatus.ReauthenticationRequired, Token);

        await Stage(host).ExecuteAsync(Token);

        (await StatusOf(host, account)).ShouldBe(AccountStatus.Connected);
    }

    [Fact]
    public async Task leave_the_status_alone_when_the_provider_cannot_be_reached()
    {
        var provider = new StubCloudProvider { ReconnectResult = new Fail<ProviderError>(new Transient("offline")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("acct-1", AccountStatus.Connected, Token);

        var result = await Stage(host).ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        (await StatusOf(host, account)).ShouldBe(AccountStatus.Connected);
    }

    [Fact]
    public async Task succeed_when_no_account_is_stored()
    {
        var provider = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(provider);
        await host.InitialiseDatabaseAsync(Token);

        var result = await Stage(host).ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        provider.Reconnected.ShouldBeEmpty();
    }

    [Fact]
    public async Task fail_startup_when_the_accounts_cannot_be_read()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());

        var result = await Stage(host).ExecuteAsync(Token);

        result.Value.ShouldBeOfType<Fail<StartupError>>().Error.StageName.ShouldBe("State recovery");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => Stage(host).ExecuteAsync(cancelled.Token));
    }

    private static IStartupStage Stage(DaemonTestHost host) => host.Services.GetServices<IStartupStage>().OfType<StateRecoveryStage>().Single();

    private static async Task<AccountStatus> StatusOf(DaemonTestHost host, Account account)
        => (await host.Services.GetRequiredService<IAccountStore>().GetAsync(account.Id, Token)).Value.ShouldBeOfType<Ok<Account>>().Value.Status;
}
