using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class AddLocalFolderScopeHandlerShould : IDisposable
{
    private readonly string localRoot = Directory.CreateTempSubdirectory("cloudsync-local-scope-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(localRoot, recursive: true);

    [Fact]
    public async Task create_the_remote_folder_for_the_job_account_and_add_it_as_a_scope()
    {
        var provider = new StubCloudProvider { CreateFolderResult = new Ok<RemoteItemId>(new RemoteItemId("photos-1")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var job = await NewJobAsync(host);

        var result = await host.Services.GetRequiredService<AddLocalFolderScopeHandler>().HandleAsync(new AddLocalFolderScope(job.Id.Value, "Photos"), Token);
        var listed = await host.Services.GetRequiredService<ISyncScopeStore>().ListAsync(job.Id, Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        provider.CreatedFolders.ShouldBe([("id", new RelativePath("Photos"))]);
        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncScope>>>().Value.ShouldBe([new SyncScope(job.Id, new RemoteItemId("photos-1"), new RelativePath("Photos"))]);
    }

    [Fact]
    public async Task reject_the_folder_when_the_remote_folder_cannot_be_created()
    {
        var provider = new StubCloudProvider { CreateFolderResult = new Fail<ProviderError>(new Transient("offline")) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var job = await NewJobAsync(host);

        var result = await host.Services.GetRequiredService<AddLocalFolderScopeHandler>().HandleAsync(new AddLocalFolderScope(job.Id.Value, "Photos"), Token);
        var listed = await host.Services.GetRequiredService<ISyncScopeStore>().ListAsync(job.Id, Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("try again", Case.Insensitive);
        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncScope>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task reject_the_folder_for_an_unknown_job_without_touching_the_drive()
    {
        var provider = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(provider);
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetRequiredService<AddLocalFolderScopeHandler>().HandleAsync(new AddLocalFolderScope(Guid.NewGuid(), "Photos"), Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("no longer exists");
        provider.CreatedFolders.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("Photos/2024")]
    public async Task reject_a_name_that_is_not_one_folder(string name)
    {
        var provider = new StubCloudProvider();
        await using var host = DaemonTestHost.CreateWith(provider);
        var job = await NewJobAsync(host);

        var result = await host.Services.GetRequiredService<AddLocalFolderScopeHandler>().HandleAsync(new AddLocalFolderScope(job.Id.Value, name), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.ValidationError);
        provider.CreatedFolders.ShouldBeEmpty();
    }

    [Fact]
    public async Task add_the_folder_over_ipc()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new AddLocalFolderScope(job.Id.Value, "Photos"), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<AddLocalFolderScopeHandler>().HandleAsync(new AddLocalFolderScope(Guid.NewGuid(), "Photos"), cancelled.Token));
    }

    private async Task<SyncJob> NewJobAsync(DaemonTestHost host)
    {
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);

        return await host.AddJobAsync(account, localRoot, Token);
    }
}
