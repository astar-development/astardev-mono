using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class RemoteFoldersQueryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task return_the_children_of_the_requested_parent()
    {
        var provider = new StubCloudProvider
        {
            ChildrenResult = new Ok<IReadOnlyList<ProviderItem>>([StubCloudProvider.Folder("f2", "Docs/zeta"), StubCloudProvider.Item("a1"), StubCloudProvider.Folder("f1", "Docs/Alpha")])
        };
        await using var host = DaemonTestHost.CreateWith(provider);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetRemoteFolders("docs-id"), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<RemoteFoldersView>>().Value.Folders.ShouldBe([new RemoteFolderView("f1", "Alpha"), new RemoteFolderView("f2", "zeta")]);
        provider.Parents.Single().Value.ShouldBe("docs-id");
    }

    [Fact]
    public async Task list_the_top_of_the_drive_when_no_parent_is_given()
    {
        var provider = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(provider);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetRemoteFolders(), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<RemoteFoldersView>>().Value.Folders.ShouldBeEmpty();
        provider.Parents.Single().Value.ShouldBe("root");
    }

    [Fact]
    public async Task fail_with_a_displayable_message_when_the_provider_cannot_list_the_folder()
    {
        var provider = new StubCloudProvider { ChildrenResult = new Fail<ProviderError>(new AuthenticationFailed("Sign in first.")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetRemoteFolders("docs-id"), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Fail<IpcError>>().Error.Value.ShouldBeOfType<HandlerFailed>().Message.ShouldBe("Sign in first.");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var query = host.Services.GetRequiredService<RemoteFoldersQuery>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => query.HandleAsync(new GetRemoteFolders(), cancelled.Token));
    }
}
