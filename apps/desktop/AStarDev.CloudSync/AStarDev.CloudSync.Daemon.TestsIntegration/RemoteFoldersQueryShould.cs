using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsIntegration;

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
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetRemoteFolders(account.Id.Value, "docs-id"), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<RemoteFoldersView>>().Value.Folders.ShouldBe([new RemoteFolderView("f1", "Alpha"), new RemoteFolderView("f2", "zeta")]);
        provider.Parents.Single().Value.ShouldBe("docs-id");
        provider.AccountsCalled.ShouldBe(["id"]);
    }

    [Fact]
    public async Task list_at_most_one_page_of_folders_with_the_total_so_the_answer_fits_an_ipc_frame()
    {
        var folders = Enumerable.Range(0, ApplicationConstants.ListLimit + 5).Select(index => (ProviderItem)StubCloudProvider.Folder($"f{index}", $"Docs/d{index:D5}")).ToList();
        var provider = new StubCloudProvider { ChildrenResult = new Ok<IReadOnlyList<ProviderItem>>(folders) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var first = await client.QueryAsync(new GetRemoteFolders(account.Id.Value, Take: 10_000), Token);
        var rest = await client.QueryAsync(new GetRemoteFolders(account.Id.Value, Skip: ApplicationConstants.ListLimit), Token);
        await host.StopAsync(Token);

        var page = first.Value.ShouldBeOfType<Ok<RemoteFoldersView>>().Value;
        (page.Total, page.Folders.Count).ShouldBe((ApplicationConstants.ListLimit + 5, ApplicationConstants.ListLimit));
        rest.Value.ShouldBeOfType<Ok<RemoteFoldersView>>().Value.Folders.Select(folder => folder.Name).ShouldBe(["d00500", "d00501", "d00502", "d00503", "d00504"]);
    }

    [Fact]
    public async Task list_the_top_of_the_drive_when_no_parent_is_given()
    {
        var provider = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetRemoteFolders(account.Id.Value), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<RemoteFoldersView>>().Value.Folders.ShouldBeEmpty();
        provider.Parents.Single().Value.ShouldBe("root");
    }

    [Fact]
    public async Task fail_with_a_displayable_message_when_the_provider_cannot_list_the_folder()
    {
        var provider = new StubCloudProvider { ChildrenResult = new Fail<ProviderError>(new AuthenticationFailed("Sign in first.")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.QueryAsync(new GetRemoteFolders(account.Id.Value, "docs-id"), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Fail<IpcError>>().Error.Value.ShouldBeOfType<HandlerFailed>().Message.ShouldBe("Sign in first.");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var query = host.Services.GetRequiredService<RemoteFoldersQuery>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => query.HandleAsync(new GetRemoteFolders(account.Id.Value), cancelled.Token));
    }

    [Fact]
    public async Task list_the_drive_of_the_account_asked_for()
    {
        var provider = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(provider);
        await host.AddAccountAsync("first", AccountStatus.Connected, Token);
        var second = await host.AddAccountAsync("second", AccountStatus.Connected, Token);
        var query = host.Services.GetRequiredService<RemoteFoldersQuery>();

        await query.HandleAsync(new GetRemoteFolders(second.Id.Value), Token);

        provider.AccountsCalled.ShouldBe(["second"]);
    }

    [Fact]
    public async Task fail_for_an_unknown_account()
    {
        var provider = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(provider);
        var query = host.Services.GetRequiredService<RemoteFoldersQuery>();

        var result = await query.HandleAsync(new GetRemoteFolders(Guid.NewGuid()), Token);

        result.Value.ShouldBeOfType<Fail<IpcError>>().Error.Value.ShouldBeOfType<HandlerFailed>();
        provider.AccountsCalled.ShouldBeEmpty();
    }
}
