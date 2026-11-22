using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.FakeProvider;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class KnownCleanupShould : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly RemoteItemId Root = new("root");
    private static readonly RelativePath FilePath = new("a.txt");

    private readonly string localRoot = Path.Combine(Path.GetTempPath(), $"cloudsync-known-cleanup-{Guid.NewGuid():N}");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(localRoot)) Directory.Delete(localRoot, recursive: true);
    }

    [Fact]
    public async Task remove_the_known_entry_when_both_sides_are_gone()
    {
        await using var host = CreateHost(new FakeCloudProvider(10));
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item);
        await DeleteRemoteAsync(host, job, "r1");

        var first = await PlanAsync(host, job);
        var second = await PlanAsync(host, job);

        first.ShouldBe(new PlanSummary(0, 0));
        second.ShouldBe(new PlanSummary(0, 0));
        (await KnownAsync(host, job)).ShouldBeEmpty();
        (await Items(host, job)).ShouldBeEmpty();
        (await Operations(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task remove_the_known_entry_of_a_local_only_item_deleted_locally()
    {
        await using var host = CreateHost(new FakeCloudProvider(10));
        var job = await SeedAsync(host);
        await RecordLocalAsync(host, job, 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item);
        (await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job, [], [FilePath], Stamp.AddHours(3), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var summary = await PlanAsync(host, job);

        summary.ShouldBe(new PlanSummary(0, 0));
        (await KnownAsync(host, job)).ShouldBeEmpty();
        (await Items(host, job)).ShouldBeEmpty();
    }

    [Fact]
    public async Task remove_the_known_entry_after_a_successful_delete()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item);
        await RecordLocalAsync(host, job, 10);
        (await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job, [], [FilePath], Stamp.AddHours(3), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await PlanAsync(host, job)).ShouldBe(new PlanSummary(1, 0));

        await RunAllAsync(host);

        provider.Deletes.ShouldHaveSingleItem();
        (await KnownAsync(host, job)).ShouldBeEmpty();
        (await Items(host, job)).ShouldBeEmpty();
        (await PlanAsync(host, job)).ShouldBe(new PlanSummary(0, 0));
    }

    [Fact]
    public async Task remove_the_known_entry_after_a_successful_local_delete()
    {
        await using var host = CreateHost(new FakeCloudProvider(10));
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item);
        await File.WriteAllTextAsync(Path.Combine(localRoot, "a.txt"), "0123456789", Token);
        await RecordLocalAsync(host, job, 10);
        await DeleteRemoteAsync(host, job, "r1");
        await RecordLocalAsync(host, job, 10);
        (await PlanAsync(host, job)).ShouldBe(new PlanSummary(1, 0));

        await RunAllAsync(host);

        File.Exists(Path.Combine(localRoot, "a.txt")).ShouldBeFalse();
        (await KnownAsync(host, job)).ShouldBeEmpty();
        (await Items(host, job)).ShouldBeEmpty();
        (await PlanAsync(host, job)).ShouldBe(new PlanSummary(0, 0));
    }

    [Fact]
    public async Task keep_the_known_entry_while_an_operation_is_pending()
    {
        await using var host = CreateHost(new FakeCloudProvider(10));
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item);
        await DeleteRemoteAsync(host, job, "r1");
        var pending = new Operation(new OperationId(Guid.NewGuid()), job, item, OperationType.DeleteLocal, OperationPriority.Background, OperationStatus.Pending, 0, Stamp, null, Stamp);
        (await host.Services.GetRequiredService<IOperationStore>().EnqueueAsync(pending, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        await PlanAsync(host, job);

        (await KnownAsync(host, job)).ShouldHaveSingleItem().ItemId.ShouldBe(item);
    }

    [Fact]
    public async Task treat_a_recreated_item_as_new()
    {
        await using var host = CreateHost(new FakeCloudProvider(10));
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item);
        await DeleteRemoteAsync(host, job, "r1");
        await PlanAsync(host, job);

        await RecordLocalAsync(host, job, 4);
        var summary = await PlanAsync(host, job);

        summary.ShouldBe(new PlanSummary(1, 0));
        (await Operations(host)).ShouldHaveSingleItem().Type.ShouldBe(OperationType.UploadFile);
        (await Items(host, job)).ShouldHaveSingleItem().ItemId.ShouldNotBe(item);
        (await KnownAsync(host, job)).ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation_when_removing_a_known_entry()
    {
        await using var host = CreateHost(new FakeCloudProvider(10));
        var job = await SeedAsync(host);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IKnownSnapshotStore>().RemoveAsync(job, new ItemId(Guid.NewGuid()), cancelled.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_planning_cleanup()
    {
        await using var host = CreateHost(new FakeCloudProvider(10));
        var job = await SeedAsync(host);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, cancelled.Token));
    }

    private DatabaseTestHost CreateHost(FakeCloudProvider provider)
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSingleton<ICloudProvider>(provider);
            services.AddSyncEngine();
        });

    private async Task<SyncJobId> SeedAsync(DatabaseTestHost host)
    {
        Directory.CreateDirectory(localRoot);
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", localRoot, Root, BootstrapMode.Merge, true, Stamp, Stamp);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, Root), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job.Id;
    }

    private static async Task AddRemoteAsync(DatabaseTestHost host, SyncJobId job, string id, long size)
    {
        var item = new ProviderItem(new RemoteItemId(id), FilePath, ItemType.File, size, Stamp);
        (await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(new SyncScope(job, Root), [item], CrawlCheckpoint.None, Stamp, Token)).Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task DeleteRemoteAsync(DatabaseTestHost host, SyncJobId job, string id)
    {
        var deleted = new ProviderChange(ChangeKind.Deleted, new RemoteItemId(id));
        (await host.Services.GetRequiredService<IItemIndexStore>().ApplyDeltaPageAsync(new SyncScope(job, Root), [deleted], null, Stamp.AddHours(3), Token)).Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task RecordLocalAsync(DatabaseTestHost host, SyncJobId job, long size)
    {
        var snapshot = new LocalSnapshot(FilePath, size, Stamp, null, new ContentFingerprint("fp" + size));
        (await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job, [snapshot], [], Stamp.AddHours(2), Token)).Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task CommitKnownAsync(DatabaseTestHost host, SyncJobId job, ItemId item)
    {
        var store = host.Services.GetRequiredService<IKnownSnapshotStore>();
        var operation = new OperationId(Guid.NewGuid());
        (await store.StageAsync(job, operation, item, new KnownSnapshot(FilePath, 10, Stamp, null, null), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await store.CommitAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task<PlanSummary> PlanAsync(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token)).Value.ShouldBeOfType<Ok<PlanSummary>>().Value;

    private static async Task RunAllAsync(DatabaseTestHost host)
    {
        await using var scheduler = host.Services.GetRequiredService<OperationScheduler>();
        while ((await scheduler.RunOnceAsync(Token)).Value is Ok<bool> { Value: true }) { }
    }

    private static async Task<IReadOnlyList<Operation>> Operations(DatabaseTestHost host)
        => (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;

    private static async Task<IReadOnlyList<StoredItem>> Items(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value;

    private static async Task<IReadOnlyList<KnownEntry>> KnownAsync(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<IKnownSnapshotStore>().ListAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<KnownEntry>>>().Value;
}
