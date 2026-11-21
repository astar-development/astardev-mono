using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class CreateSyncJobHandlerShould : IDisposable
{
    private readonly string localRoot = Directory.CreateTempSubdirectory("cloudsync-job-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(localRoot, recursive: true);

    [Fact]
    public async Task store_the_job_with_the_chosen_bootstrap_mode()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var handler = host.Services.GetRequiredService<CreateSyncJobHandler>();

        var result = await handler.HandleAsync(new CreateSyncJob(account.Id.Value, localRoot, SyncBootstrapMode.Download), Token);
        var stored = await host.Services.GetRequiredService<ISyncJobStore>().ListAsync(Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        var job = stored.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncJob>>>().Value.ShouldHaveSingleItem();
        job.AccountId.ShouldBe(account.Id);
        job.LocalRootPath.ShouldBe(localRoot);
        job.BootstrapMode.ShouldBe(BootstrapMode.Download);
        job.RemoteRootId.ShouldBe(new RemoteItemId("root"));
        job.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task reject_a_second_job()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        await host.AddJobAsync(account, localRoot, Token);
        var handler = host.Services.GetRequiredService<CreateSyncJobHandler>();

        var result = await handler.HandleAsync(new CreateSyncJob(account.Id.Value, localRoot, SyncBootstrapMode.Merge), Token);
        var stored = await host.Services.GetRequiredService<ISyncJobStore>().ListAsync(Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("already");
        stored.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncJob>>>().Value.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task reject_a_local_root_that_does_not_exist()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var handler = host.Services.GetRequiredService<CreateSyncJobHandler>();

        var result = await handler.HandleAsync(new CreateSyncJob(account.Id.Value, Path.Combine(localRoot, "missing"), SyncBootstrapMode.Merge), Token);
        var stored = await host.Services.GetRequiredService<ISyncJobStore>().ListAsync(Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.ValidationError);
        reply.Message!.ShouldContain("does not exist");
        stored.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncJob>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task reject_a_local_root_that_is_not_an_absolute_path()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        var handler = host.Services.GetRequiredService<CreateSyncJobHandler>();

        var result = await handler.HandleAsync(new CreateSyncJob(account.Id.Value, ".", SyncBootstrapMode.Merge), Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.ValidationError);
        reply.Message!.ShouldContain("absolute");
    }

    [Fact]
    public async Task reject_an_account_that_is_not_connected()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);
        var handler = host.Services.GetRequiredService<CreateSyncJobHandler>();

        var result = await handler.HandleAsync(new CreateSyncJob(Guid.NewGuid(), localRoot, SyncBootstrapMode.Merge), Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("no longer connected");
    }

    [Fact]
    public async Task answer_over_ipc()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var result = await client.SendAsync(new CreateSyncJob(account.Id.Value, localRoot, SyncBootstrapMode.Upload), Token);
        var jobs = await client.QueryAsync(new GetSyncJobs(), Token);
        await host.StopAsync(Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        var view = jobs.Value.ShouldBeOfType<Ok<SyncJobsView>>().Value.Jobs.ShouldHaveSingleItem();
        view.LocalRootPath.ShouldBe(localRoot);
        view.BootstrapMode.ShouldBe(SyncBootstrapMode.Upload);
        view.AccountId.ShouldBe(account.Id.Value);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var handler = host.Services.GetRequiredService<CreateSyncJobHandler>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => handler.HandleAsync(new CreateSyncJob(Guid.NewGuid(), localRoot, SyncBootstrapMode.Merge), cancelled.Token));
    }
}
