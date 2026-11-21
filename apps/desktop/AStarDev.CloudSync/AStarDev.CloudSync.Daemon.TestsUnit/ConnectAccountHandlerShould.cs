using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ConnectAccountHandlerShould
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task publish_account_connected_after_connecting()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        var published = new TaskCompletionSource<AccountConnected>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnEvent<AccountConnected>(connected => published.TrySetResult(connected));
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await client.SubscribeAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new ConnectAccount(), Token);
        var connected = await published.Task.WaitAsync(Timeout, Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        connected.DisplayName.ShouldBe("Ada");
    }

    [Fact]
    public async Task store_the_account_after_connecting()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        await client.SendAsync(new ConnectAccount(), Token);
        var stored = await host.Services.GetRequiredService<IAccountStore>().ListAsync(Token);
        await host.StopAsync(Token);

        var account = stored.Value.ShouldBeOfType<Ok<IReadOnlyList<Account>>>().Value.ShouldHaveSingleItem();
        account.ProviderType.ShouldBe("OneDrive");
        account.DisplayName.ShouldBe("Ada");
        account.AccountIdentifier.ShouldBe("id");
        account.Status.ShouldBe(AccountStatus.Connected);
    }

    [Fact]
    public async Task create_no_sync_job()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new ConnectAccount(), Token);
        var jobs = await host.Services.GetRequiredService<ISyncJobStore>().ListAsync(Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        jobs.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncJob>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task mark_a_known_account_connected_again_instead_of_adding_a_second()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var known = await host.AddAccountAsync("id", AccountStatus.ReauthenticationRequired, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new ConnectAccount(), Token);
        var stored = await host.Services.GetRequiredService<IAccountStore>().ListAsync(Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        stored.Value.ShouldBeOfType<Ok<IReadOnlyList<Account>>>().Value.ShouldBe([known with { Status = AccountStatus.Connected }]);
    }

    [Fact]
    public async Task reject_when_the_account_cannot_be_stored()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var handler = host.Services.GetRequiredService<ConnectAccountHandler>();

        var result = await handler.HandleAsync(new ConnectAccount(), Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("could not be saved");
    }

    [Fact]
    public async Task reject_when_authentication_fails()
    {
        var provider = new StubCloudProvider { ConnectResult = new Fail<ProviderError>(new AuthenticationFailed("The Secret Service keyring is unavailable.")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new ConnectAccount(), Token);
        await host.StopAsync(Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message.ShouldBe("The Secret Service keyring is unavailable.");
    }

    [Fact]
    public async Task reject_with_advice_to_retry_when_the_provider_is_temporarily_unavailable()
    {
        var provider = new StubCloudProvider { ConnectResult = new Fail<ProviderError>(new Transient("offline")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new ConnectAccount(), Token);
        await host.StopAsync(Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("offline");
        reply.Message!.ShouldContain("try again", Case.Insensitive);
    }

    [Fact]
    public async Task reject_when_the_onedrive_client_id_is_not_configured()
    {
        await using var host = DaemonTestHost.Create();
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new ConnectAccount(), Token);
        await host.StopAsync(Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("client id");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var handler = host.Services.GetRequiredService<ConnectAccountHandler>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => handler.HandleAsync(new ConnectAccount(), cancelled.Token));
    }
}
