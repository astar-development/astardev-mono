using AStarDev.CloudSync.Daemon;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration.DaemonTests;

public sealed class ConflictQueryShould : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);

    private readonly string localRoot = Directory.CreateTempSubdirectory("cloudsync-conflicts-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(localRoot, recursive: true);

    [Fact]
    public async Task list_open_conflicts()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        var store = host.Services.GetRequiredService<IConflictStore>();
        var older = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.ModifyModify, ConflictStatus.Open, Stamp);
        var newer = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.KeepDelete, ConflictStatus.Open, Stamp.AddMinutes(5));
        var resolved = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.RenameRename, ConflictStatus.Resolved, Stamp.AddMinutes(1));
        await store.AddAsync(newer, Token);
        await store.AddAsync(resolved, Token);
        await store.AddAsync(older, Token);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);

        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var view = (await client.QueryAsync(new GetConflicts(job.Id.Value), Token)).Value.ShouldBeOfType<Ok<ConflictsView>>().Value;
        await host.StopAsync(Token);

        view.Conflicts.ShouldBe(
        [
            new ConflictView(older.Id.Value, job.Id.Value, older.ItemId.Value, "ModifyModify", Stamp, "Open", string.Empty, null, null),
            new ConflictView(newer.Id.Value, job.Id.Value, newer.ItemId.Value, "KeepDelete", Stamp.AddMinutes(5), "Open", string.Empty, null, null)
        ]);
    }

    [Fact]
    public async Task list_one_page_of_the_conflicts_oldest_first_with_how_many_the_job_has()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        var store = host.Services.GetRequiredService<IConflictStore>();
        var conflicts = Enumerable.Range(0, 3).Select(minutes => new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.ModifyModify, ConflictStatus.Open, Stamp.AddMinutes(minutes))).ToList();
        foreach (var conflict in conflicts)
            await store.AddAsync(conflict, Token);

        var result = await host.Services.GetRequiredService<ConflictsQuery>().HandleAsync(new GetConflicts(job.Id.Value, 1, 1), Token);

        var page = result.Value.ShouldBeOfType<Ok<ConflictsView>>().Value;
        page.Total.ShouldBe(3);
        page.Conflicts.ShouldHaveSingleItem().ConflictId.ShouldBe(conflicts[1].Id.Value);
    }

    [Fact]
    public async Task list_a_deferred_conflict_with_its_status()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        var deferred = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.ModifyModify, ConflictStatus.Deferred, Stamp);
        await host.Services.GetRequiredService<IConflictStore>().AddAsync(deferred, Token);

        var result = await host.Services.GetRequiredService<ConflictsQuery>().HandleAsync(new GetConflicts(job.Id.Value), Token);

        result.Value.ShouldBeOfType<Ok<ConflictsView>>().Value.Conflicts.ShouldHaveSingleItem().Status.ShouldBe("Deferred");
    }

    [Fact]
    public async Task describe_the_item_path_and_both_sides()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        var conflict = await SeedConflictingItemAsync(host, job);

        var result = await host.Services.GetRequiredService<ConflictsQuery>().HandleAsync(new GetConflicts(job.Id.Value), Token);

        var view = result.Value.ShouldBeOfType<Ok<ConflictsView>>().Value.Conflicts.ShouldHaveSingleItem();
        view.ConflictId.ShouldBe(conflict.Id.Value);
        view.Status.ShouldBe("Open");
        view.Path.ShouldBe("Reports/a.txt");
        view.Local.ShouldBe(new ConflictSideView("Reports/a.txt", Stamp.AddHours(1), 20));
        view.Remote.ShouldBe(new ConflictSideView("Reports/a.txt", Stamp, 10));
    }

    [Fact]
    public async Task describe_a_deleted_side_as_absent()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        var known = host.Services.GetRequiredService<IKnownSnapshotStore>();
        var item = new ItemId(Guid.NewGuid());
        var operation = new OperationId(Guid.NewGuid());
        await known.StageAsync(job.Id, operation, item, new KnownSnapshot(new RelativePath("gone.txt"), 5, Stamp, null, null), Token);
        await known.CommitAsync(operation, Token);
        await host.Services.GetRequiredService<IConflictStore>().AddAsync(new Conflict(new ConflictId(Guid.NewGuid()), job.Id, item, ConflictType.KeepDelete, ConflictStatus.Open, Stamp), Token);

        var result = await host.Services.GetRequiredService<ConflictsQuery>().HandleAsync(new GetConflicts(job.Id.Value), Token);

        var view = result.Value.ShouldBeOfType<Ok<ConflictsView>>().Value.Conflicts.ShouldHaveSingleItem();
        view.Path.ShouldBe("gone.txt");
        view.Local.ShouldBeNull();
        view.Remote.ShouldBeNull();
    }

    [Fact]
    public async Task publish_conflict_detected_with_the_path_and_sides()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        var conflict = await SeedConflictingItemAsync(host, job);
        var detected = new TaskCompletionSource<ConflictDetected>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        client.OnEvent<ConflictDetected>(@event => detected.TrySetResult(@event));

        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await client.SubscribeAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        await host.Services.GetRequiredService<IConflictEventSink>().DetectedAsync(conflict, Token);
        var detectedEvent = await detected.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await host.StopAsync(Token);

        detectedEvent.Conflict.Path.ShouldBe("Reports/a.txt");
        detectedEvent.Conflict.Local.ShouldBe(new ConflictSideView("Reports/a.txt", Stamp.AddHours(1), 20));
        detectedEvent.Conflict.Remote.ShouldBe(new ConflictSideView("Reports/a.txt", Stamp, 10));
    }

    [Fact]
    public async Task list_nothing_for_an_unknown_job()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetRequiredService<ConflictsQuery>().HandleAsync(new GetConflicts(Guid.NewGuid()), Token);

        result.Value.ShouldBeOfType<Ok<ConflictsView>>().Value.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public async Task publish_conflict_detected_and_conflict_resolved_to_a_subscriber()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var job = await NewJobAsync(host);
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), ConflictType.CreateCreate, ConflictStatus.Open, Stamp);
        var detected = new TaskCompletionSource<ConflictDetected>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolved = new TaskCompletionSource<ConflictResolved>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        client.OnEvent<ConflictDetected>(@event => detected.TrySetResult(@event));
        client.OnEvent<ConflictResolved>(@event => resolved.TrySetResult(@event));

        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await client.SubscribeAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var sink = host.Services.GetRequiredService<IConflictEventSink>();
        await sink.DetectedAsync(conflict, Token);
        await sink.ResolvedAsync(conflict with { Status = ConflictStatus.Resolved }, "KeepLocal", Stamp.AddMinutes(2), Token);
        var detectedEvent = await detected.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        var resolvedEvent = await resolved.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await host.StopAsync(Token);

        detectedEvent.Conflict.ShouldBe(new ConflictView(conflict.Id.Value, job.Id.Value, conflict.ItemId.Value, "CreateCreate", Stamp, "Open", string.Empty, null, null));
        resolvedEvent.ShouldBe(new ConflictResolved(conflict.Id.Value, job.Id.Value, conflict.ItemId.Value, "KeepLocal", Stamp.AddMinutes(2)));
    }

    [Fact]
    public async Task register_the_ipc_sink_for_conflict_events()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());

        host.Services.GetRequiredService<IConflictEventSink>().ShouldBeOfType<IpcConflictEventSink>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ConflictsQuery>().HandleAsync(new GetConflicts(Guid.NewGuid()), cancelled.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_reading_the_item_state()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), new SyncJobId(Guid.NewGuid()), new ItemId(Guid.NewGuid()), ConflictType.CreateCreate, ConflictStatus.Open, Stamp);

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<CanonicalStateReader>().ReadAsync(conflict.SyncJobId, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ConflictViewFactory>().CreateAsync(conflict.SyncJobId, [conflict], cancelled.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_publishing_an_event()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), new SyncJobId(Guid.NewGuid()), new ItemId(Guid.NewGuid()), ConflictType.CreateCreate, ConflictStatus.Open, Stamp);

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IConflictEventSink>().DetectedAsync(conflict, cancelled.Token));
    }

    private static async Task<Conflict> SeedConflictingItemAsync(DaemonTestHost host, SyncJob job)
    {
        var scope = new SyncScope(job.Id, new RemoteItemId("root"));
        await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(scope, Token);
        var remote = new ProviderItem(new RemoteItemId("r1"), new RelativePath("Reports/a.txt"), ItemType.File, 10, Stamp);
        await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(scope, [remote], CrawlCheckpoint.None, Stamp, Token);
        var local = new LocalSnapshot(new RelativePath("Reports/a.txt"), 20, Stamp.AddHours(1), null, new ContentFingerprint("fp20"));
        await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job.Id, [local], [], Stamp.AddHours(2), Token);
        var item = (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job.Id, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value.Single().ItemId;
        var known = host.Services.GetRequiredService<IKnownSnapshotStore>();
        var operation = new OperationId(Guid.NewGuid());
        await known.StageAsync(job.Id, operation, item, new KnownSnapshot(new RelativePath("Reports/a.txt"), 5, Stamp.AddDays(-1), null, null), Token);
        await known.CommitAsync(operation, Token);
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), job.Id, item, ConflictType.ModifyModify, ConflictStatus.Open, Stamp);
        await host.Services.GetRequiredService<IConflictStore>().AddAsync(conflict, Token);

        return conflict;
    }

    private async Task<SyncJob> NewJobAsync(DaemonTestHost host)
    {
        var account = await host.AddAccountAsync("id", AccountStatus.Connected, Token);

        return await host.AddJobAsync(account, localRoot, Token, enabled: false);
    }
}
