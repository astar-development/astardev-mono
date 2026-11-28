using AStarDev.CloudSync.Daemon;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration.DaemonTests;

public sealed class AccountsQueryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_a_connected_account()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("one", AccountStatus.Connected, Token);

        var accounts = await ListAsync(host);

        accounts.ShouldBe([new AccountView(account.Id.Value, "one@example.com", "OneDrive", AccountState.Connected)]);
    }

    [Fact]
    public async Task list_an_account_whose_sign_in_was_refused_as_needing_reauthentication()
    {
        var provider = new StubCloudProvider { ReconnectResult = new Fail<ProviderError>(new AuthenticationFailed("expired")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("two", AccountStatus.Connected, Token);

        var accounts = await ListAsync(host);

        accounts.ShouldBe([new AccountView(account.Id.Value, "two@example.com", "OneDrive", AccountState.ReauthenticationRequired)]);
    }

    [Fact]
    public async Task answer_with_no_accounts_when_none_is_connected()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetAccounts(), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<AccountsView>>().Value.Accounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var query = host.Services.GetRequiredService<AccountsQuery>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => query.HandleAsync(new GetAccounts(), cancelled.Token));
    }

    private static async Task<IReadOnlyList<AccountView>> ListAsync(DaemonTestHost host)
    {
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetAccounts(), Token);
        await host.StopAsync(Token);

        return result.Value.ShouldBeOfType<Ok<AccountsView>>().Value.Accounts;
    }
}
