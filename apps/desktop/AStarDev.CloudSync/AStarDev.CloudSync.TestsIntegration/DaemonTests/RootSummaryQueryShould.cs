using AStarDev.CloudSync.Daemon;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration.DaemonTests;

public sealed class RootSummaryQueryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task return_the_item_count_from_the_provider()
    {
        var provider = new StubCloudProvider { SnapshotResult = new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage([StubCloudProvider.Item("a"), StubCloudProvider.Item("b"), StubCloudProvider.Item("c")])) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetRemoteRootSummary(account.Id.Value), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<RemoteRootSummaryView>>().Value.ItemCount.ShouldBe(3);
        provider.Roots.Single().Value.ShouldBe("root");
    }

    [Fact]
    public async Task fail_with_a_displayable_message_when_the_provider_cannot_list_the_root()
    {
        var provider = new StubCloudProvider { SnapshotResult = new Fail<ProviderError>(new AuthenticationFailed("Sign in first.")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetRemoteRootSummary(account.Id.Value), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Fail<IpcError>>().Error.Value.ShouldBeOfType<HandlerFailed>().Message.ShouldBe("Sign in first.");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var query = host.Services.GetRequiredService<RootSummaryQuery>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => query.HandleAsync(new GetRemoteRootSummary(account.Id.Value), cancelled.Token));
    }
}
