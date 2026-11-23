using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class PlannerShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly RemoteItemId Root = new("root");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task queue_a_download_for_a_remote_create()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "docs/a.txt", 10);

        var summary = await PlanAsync(host, job);

        summary.ShouldBe(new PlanSummary(1, 0));
        var operation = (await Operations(host)).ShouldHaveSingleItem();
        operation.Type.ShouldBe(OperationType.DownloadFile);
        operation.Priority.ShouldBe(OperationPriority.Background);
        operation.Status.ShouldBe(OperationStatus.Pending);
    }

    [Fact]
    public async Task queue_nothing_for_a_known_remote_file_the_local_scan_has_not_seen_yet()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item, new KnownSnapshot(new RelativePath("a.txt"), 10, Stamp, null, null));

        var summary = await PlanAsync(host, job);

        summary.ShouldBe(new PlanSummary(0, 0));
        (await Operations(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task queue_a_remote_delete_once_the_local_scan_recorded_the_file_gone()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 10);
        await RecordLocalAsync(host, job, "a.txt", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item, new KnownSnapshot(new RelativePath("a.txt"), 10, Stamp, null, new ContentFingerprint("fp10")));
        (await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job, [], [new RelativePath("a.txt")], Stamp.AddHours(3), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        await PlanAsync(host, job);

        (await Operations(host)).ShouldHaveSingleItem().Type.ShouldBe(OperationType.DeleteRemote);
    }

    [Fact]
    public async Task persist_a_conflict_for_modify_modify()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 10);
        await RecordLocalAsync(host, job, "a.txt", 20);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item, new KnownSnapshot(new RelativePath("a.txt"), 5, Stamp.AddDays(-1), null, null));

        var summary = await PlanAsync(host, job);
        await PlanAsync(host, job);

        summary.ShouldBe(new PlanSummary(0, 1));
        var conflicts = (await host.Services.GetRequiredService<IConflictStore>().ListOpenAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Conflict>>>().Value;
        var conflict = conflicts.ShouldHaveSingleItem();
        conflict.ItemId.ShouldBe(item);
        conflict.Type.ShouldBe(ConflictType.ModifyModify);
        conflict.Status.ShouldBe(ConflictStatus.Open);
        (await Operations(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task commit_the_known_snapshot_only_after_success()
    {
        var attempts = 0;
        var executor = new ScriptedOperationExecutor(_ => ++attempts == 1 ? new Fail<ProviderError>(new Permanent("denied")) : new Ok<Unit>(default));
        await using var host = CreateHost(executor);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 10);
        await PlanAsync(host, job);
        await using var scheduler = host.Services.GetRequiredService<OperationScheduler>();

        await scheduler.RunOnceAsync(Token);
        var afterFailure = await KnownAsync(host, job);
        var dead = (await Operations(host)).ShouldHaveSingleItem();
        await host.Services.GetRequiredService<IOperationStore>().RequeueDeadLetterAsync(dead.Id, Token);
        await scheduler.RunOnceAsync(Token);
        var afterSuccess = await KnownAsync(host, job);

        dead.Status.ShouldBe(OperationStatus.DeadLetter);
        afterFailure.ShouldBeEmpty();
        var known = afterSuccess.ShouldHaveSingleItem().Snapshot;
        known.Path.ShouldBe(new RelativePath("a.txt"));
        known.Size.ShouldBe(10);
    }

    [Fact]
    public async Task not_queue_the_same_work_twice()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 10);

        var first = await PlanAsync(host, job);
        var second = await PlanAsync(host, job);

        first.ShouldBe(new PlanSummary(1, 0));
        second.ShouldBe(new PlanSummary(0, 0));
        (await Operations(host)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task merge_a_local_only_item_with_a_remote_item_at_the_same_path()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await RecordLocalAsync(host, job, "a.txt", 3);
        await AddRemoteAsync(host, job, "r1", "a.txt", 3);

        var summary = await PlanAsync(host, job);

        summary.ShouldBe(new PlanSummary(0, 1));
        (await Operations(host)).ShouldBeEmpty();
        var conflict = (await host.Services.GetRequiredService<IConflictStore>().ListOpenAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Conflict>>>().Value.ShouldHaveSingleItem();
        conflict.Type.ShouldBe(ConflictType.CreateCreate);
        conflict.ItemId.ShouldBe((await Items(host, job)).Single(item => item.RemoteItemId.Value.Length > 0).ItemId);
    }

    [Fact]
    public async Task plan_a_rename_then_an_update_for_a_local_rename_with_a_remote_change()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "old/a.txt", 99);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item, new KnownSnapshot(new RelativePath("old/a.txt"), 10, Stamp, null, new ContentFingerprint("fp10")));
        await RecordLocalAsync(host, job, "new/a.txt", 10, Stamp);

        var summary = await PlanAsync(host, job);

        summary.ShouldBe(new PlanSummary(2, 0));
        var operations = await Operations(host);
        operations.Select(operation => operation.Type).ShouldBe([OperationType.RenameRemote, OperationType.DownloadFile]);
        operations.ShouldAllBe(operation => operation.ItemId == item);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, cancelled.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_committing_a_known_snapshot()
    {
        await using var host = CreateHost();
        await SeedAsync(host);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IKnownSnapshotStore>().CommitAsync(new OperationId(Guid.NewGuid()), cancelled.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_adding_a_conflict()
    {
        await using var host = CreateHost();
        var job = await SeedAsync(host);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var conflict = new Conflict(new ConflictId(Guid.NewGuid()), job, new ItemId(Guid.NewGuid()), ConflictType.ModifyModify, ConflictStatus.Open, Stamp);

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IConflictStore>().AddAsync(conflict, cancelled.Token));
    }

    private static DatabaseTestHost CreateHost(IOperationExecutor? executor = null)
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSingleton(executor ?? new ScriptedOperationExecutor(_ => new Ok<Unit>(default)));
            services.AddSyncEngine();
        });

    private static async Task<SyncJobId> SeedAsync(DatabaseTestHost host)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", Root, BootstrapMode.Merge, true, Stamp, Stamp);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, Root), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job.Id;
    }

    private static async Task AddRemoteAsync(DatabaseTestHost host, SyncJobId job, string id, string path, long size)
    {
        var item = new ProviderItem(new RemoteItemId(id), new RelativePath(path), ItemType.File, size, Stamp);
        var saved = await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(new SyncScope(job, Root), [item], CrawlCheckpoint.None, Stamp, Token);
        saved.Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task RecordLocalAsync(DatabaseTestHost host, SyncJobId job, string path, long size, DateTimeOffset? modified = null)
    {
        var snapshot = new LocalSnapshot(new RelativePath(path), size, modified ?? Stamp.AddHours(1), null, new ContentFingerprint("fp" + size));
        var recorded = await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job, [snapshot], [], Stamp.AddHours(2), Token);
        recorded.Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task CommitKnownAsync(DatabaseTestHost host, SyncJobId job, ItemId item, KnownSnapshot known)
    {
        var store = host.Services.GetRequiredService<IKnownSnapshotStore>();
        var operation = new OperationId(Guid.NewGuid());
        (await store.StageAsync(job, operation, item, known, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await store.CommitAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task<PlanSummary> PlanAsync(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token)).Value.ShouldBeOfType<Ok<PlanSummary>>().Value;

    private static async Task<IReadOnlyList<Operation>> Operations(DatabaseTestHost host)
        => (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;

    private static async Task<IReadOnlyList<StoredItem>> Items(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value;

    private static async Task<IReadOnlyList<KnownEntry>> KnownAsync(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<IKnownSnapshotStore>().ListAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<KnownEntry>>>().Value;
}
