using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class StartSyncHandlerShould : IDisposable
{
    private readonly string localRoot = Directory.CreateTempSubdirectory("cloudsync-start-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(localRoot, recursive: true);

    [Fact]
    public async Task crawl_the_scopes_of_the_job_in_the_background()
    {
        var provider = new StubCloudProvider { SnapshotResult = new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage([StubCloudProvider.Item("a")], null, new DeltaToken("delta-1"))) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var job = await NewJobAsync(host);
        var scope = new SyncScope(job.Id, new RemoteItemId("docs"));
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(scope, Token);

        var result = await host.Services.GetRequiredService<StartSyncHandler>().HandleAsync(new StartSync(job.Id.Value), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        (await CompletedAsync(host, scope)).ShouldBe(new CrawlCheckpoint(null, 1, true, new DeltaToken("delta-1")));
    }

    [Fact]
    public async Task plan_the_crawled_items_into_operations()
    {
        var provider = new StubCloudProvider { SnapshotResult = new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage([StubCloudProvider.Item("a")], null, new DeltaToken("delta-1"))) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var job = await NewJobAsync(host);
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, new RemoteItemId("docs")), Token);

        await host.Services.GetRequiredService<StartSyncHandler>().HandleAsync(new StartSync(job.Id.Value), Token);

        (await QueuedAsync(host)).Select(operation => operation.Type).ShouldBe([OperationType.DownloadFile]);
    }

    [Fact]
    public async Task accept_a_job_that_is_already_running()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        var handler = host.Services.GetRequiredService<StartSyncHandler>();
        await handler.HandleAsync(new StartSync(job.Id.Value), Token);

        var result = await handler.HandleAsync(new StartSync(job.Id.Value), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
    }

    [Fact]
    public async Task reject_an_unknown_job()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetRequiredService<StartSyncHandler>().HandleAsync(new StartSync(Guid.NewGuid()), Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("no longer exists");
    }

    [Fact]
    public async Task stop_a_crawl_on_request_and_keep_its_checkpoint_for_the_next_start()
    {
        var provider = new StubCloudProvider { SnapshotResult = new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage([StubCloudProvider.Item("a")], "next", null)) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var job = await NewJobAsync(host);
        var scope = new SyncScope(job.Id, new RemoteItemId("docs"));
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(scope, Token);
        await host.Services.GetRequiredService<StartSyncHandler>().HandleAsync(new StartSync(job.Id.Value), Token);

        var stopped = await host.Services.GetRequiredService<StopSyncHandler>().HandleAsync(new StopSync(job.Id.Value), Token);
        var checkpoint = await host.Services.GetRequiredService<IItemIndexStore>().GetCheckpointAsync(scope, Token);

        stopped.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        checkpoint.Value.ShouldBeOfType<Ok<CrawlCheckpoint>>().Value.Completed.ShouldBeFalse();
    }

    [Fact]
    public async Task accept_a_stop_when_nothing_is_running()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());

        var result = await host.Services.GetRequiredService<StopSyncHandler>().HandleAsync(new StopSync(Guid.NewGuid()), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<StartSyncHandler>().HandleAsync(new StartSync(Guid.NewGuid()), cancelled.Token));
    }

    private async Task<SyncJob> NewJobAsync(DaemonTestHost host)
    {
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);

        return await host.AddJobAsync(account, localRoot, Token);
    }

    private static async Task<CrawlCheckpoint> CompletedAsync(DaemonTestHost host, SyncScope scope)
    {
        var store = host.Services.GetRequiredService<IItemIndexStore>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        var checkpoint = CrawlCheckpoint.None;

        while (!checkpoint.Completed && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, Token);
            checkpoint = (await store.GetCheckpointAsync(scope, Token)).Value.ShouldBeOfType<Ok<CrawlCheckpoint>>().Value;
        }

        return checkpoint;
    }

    private static async Task<IReadOnlyList<Operation>> QueuedAsync(DaemonTestHost host)
    {
        var store = host.Services.GetRequiredService<IOperationStore>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        IReadOnlyList<Operation> operations = [];

        while (operations.Count == 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20, Token);
            operations = (await store.ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;
        }

        return operations;
    }
}
