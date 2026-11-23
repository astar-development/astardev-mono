using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class SyncScopeHandlersShould : IDisposable
{
    private readonly string localRoot = Directory.CreateTempSubdirectory("cloudsync-scope-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(localRoot, recursive: true);

    [Fact]
    public async Task add_a_scope_to_the_job()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);

        var result = await host.Services.GetRequiredService<AddSyncScopeHandler>().HandleAsync(new AddSyncScope(job.Id.Value, "docs"), Token);
        var listed = await host.Services.GetRequiredService<ISyncScopeStore>().ListAsync(job.Id, Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncScope>>>().Value.ShouldBe([new SyncScope(job.Id, new RemoteItemId("docs"))]);
    }

    [Fact]
    public async Task record_a_scope_change_in_the_journal()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);

        await host.Services.GetRequiredService<AddSyncScopeHandler>().HandleAsync(new AddSyncScope(job.Id.Value, "docs"), Token);
        await host.Services.GetRequiredService<RemoveSyncScopeHandler>().HandleAsync(new RemoveSyncScope(job.Id.Value, "docs"), Token);
        var entries = await host.Services.GetRequiredService<IEventJournal>().ListAsync(job.Id.Value, Token);

        var listed = entries.Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value;
        listed.Select(entry => entry.EventType).ShouldBe(["SyncScopeAdded", "SyncScopeRemoved"]);
        listed[0].PayloadJson.ShouldContain("docs");
    }

    [Fact]
    public async Task start_the_crawl_when_the_first_scope_is_added()
    {
        var provider = new StubCloudProvider { SnapshotResult = new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage([StubCloudProvider.Item("a")], null, new DeltaToken("delta-1"))) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var job = await NewJobAsync(host);

        await host.Services.GetRequiredService<AddSyncScopeHandler>().HandleAsync(new AddSyncScope(job.Id.Value, "docs"), Token);

        (await CompletedAsync(host, new SyncScope(job.Id, new RemoteItemId("docs")))).Completed.ShouldBeTrue();
    }

    [Fact]
    public async Task crawl_a_scope_added_later_without_waiting_for_the_polling_interval()
    {
        var provider = new StubCloudProvider { SnapshotResult = new Ok<ProviderSnapshotPage>(new ProviderSnapshotPage([StubCloudProvider.Item("a")], null, new DeltaToken("delta-1"))) };
        await using var host = DaemonTestHost.CreateWith(provider);
        var job = await NewJobAsync(host);
        var handler = host.Services.GetRequiredService<AddSyncScopeHandler>();
        await handler.HandleAsync(new AddSyncScope(job.Id.Value, "docs"), Token);
        (await CompletedAsync(host, new SyncScope(job.Id, new RemoteItemId("docs")))).Completed.ShouldBeTrue();

        await handler.HandleAsync(new AddSyncScope(job.Id.Value, "notes"), Token);

        (await CompletedAsync(host, new SyncScope(job.Id, new RemoteItemId("notes")))).Completed.ShouldBeTrue();
    }

    [Fact]
    public async Task reject_a_scope_for_an_unknown_job()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetRequiredService<AddSyncScopeHandler>().HandleAsync(new AddSyncScope(Guid.NewGuid(), "docs"), Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("no longer exists");
    }

    [Fact]
    public async Task reject_a_scope_the_job_already_holds()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        var handler = host.Services.GetRequiredService<AddSyncScopeHandler>();
        await handler.HandleAsync(new AddSyncScope(job.Id.Value, "docs"), Token);

        var result = await handler.HandleAsync(new AddSyncScope(job.Id.Value, "docs"), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Rejected);
    }

    [Fact]
    public async Task remove_a_scope_from_the_job()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        await host.Services.GetRequiredService<AddSyncScopeHandler>().HandleAsync(new AddSyncScope(job.Id.Value, "docs"), Token);

        var result = await host.Services.GetRequiredService<RemoveSyncScopeHandler>().HandleAsync(new RemoveSyncScope(job.Id.Value, "docs"), Token);
        var listed = await host.Services.GetRequiredService<ISyncScopeStore>().ListAsync(job.Id, Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncScope>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task reject_removing_a_scope_the_job_does_not_hold()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);

        var result = await host.Services.GetRequiredService<RemoveSyncScopeHandler>().HandleAsync(new RemoveSyncScope(job.Id.Value, "docs"), Token);

        var reply = result.Value.ShouldBeOfType<Ok<CommandResult>>().Value;
        reply.Outcome.ShouldBe(CommandOutcome.Rejected);
        reply.Message!.ShouldContain("not selected");
    }

    [Fact]
    public async Task list_the_scopes_of_a_job_over_ipc()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        await client.SendAsync(new AddSyncScope(job.Id.Value, "notes"), Token);
        await client.SendAsync(new AddSyncScope(job.Id.Value, "docs"), Token);
        var scopes = await client.QueryAsync(new GetSyncScopes(job.Id.Value), Token);
        await host.StopAsync(Token);

        scopes.Value.ShouldBeOfType<Ok<SyncScopesView>>().Value.Scopes.ShouldBe([new SyncScopeView("docs"), new SyncScopeView("notes")]);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<AddSyncScopeHandler>().HandleAsync(new AddSyncScope(Guid.NewGuid(), "docs"), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<RemoveSyncScopeHandler>().HandleAsync(new RemoveSyncScope(Guid.NewGuid(), "docs"), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SyncScopesQuery>().HandleAsync(new GetSyncScopes(Guid.NewGuid()), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SyncJobsQuery>().HandleAsync(new GetSyncJobs(), cancelled.Token));
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
}
