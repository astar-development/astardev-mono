using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class DisconnectAccountHandlerShould
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task remove_the_account_and_its_token()
    {
        var provider = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("acct-1", AccountStatus.Connected, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        var published = new TaskCompletionSource<AccountDisconnected>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnEvent<AccountDisconnected>(disconnected => published.TrySetResult(disconnected));
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await client.SubscribeAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new DisconnectAccount(account.Id.Value), Token);
        var disconnected = await published.Task.WaitAsync(Timeout, Token);
        var remaining = await host.Services.GetRequiredService<IAccountStore>().GetAsync(account.Id, Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        disconnected.AccountId.ShouldBe(account.Id.Value);
        provider.Disconnected.ShouldBe(["acct-1"]);
        remaining.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new AccountNotFound(account.Id));
    }

    [Fact]
    public async Task reject_an_unknown_account()
    {
        var provider = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(provider);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new DisconnectAccount(Guid.NewGuid()), Token);
        await host.StopAsync(Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("no longer connected");
        provider.Disconnected.ShouldBeEmpty();
    }

    [Fact]
    public async Task keep_the_account_when_its_token_cannot_be_removed()
    {
        var provider = new StubCloudProvider { DisconnectResult = new Fail<ProviderError>(new AuthenticationFailed("The Secret Service keyring is unavailable.")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("acct-1", AccountStatus.Connected, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new DisconnectAccount(account.Id.Value), Token);
        var remaining = await host.Services.GetRequiredService<IAccountStore>().GetAsync(account.Id, Token);
        await host.StopAsync(Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message.ShouldBe("The Secret Service keyring is unavailable.");
        remaining.Value.ShouldBeOfType<Ok<Account>>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var handler = host.Services.GetRequiredService<DisconnectAccountHandler>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => handler.HandleAsync(new DisconnectAccount(Guid.NewGuid()), cancelled.Token));
    }
}
