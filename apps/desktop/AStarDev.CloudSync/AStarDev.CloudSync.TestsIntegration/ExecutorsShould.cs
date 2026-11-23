using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.FakeProvider;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class ExecutorsShould : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly RemoteItemId Root = new("root");

    private readonly string localRoot = Path.Combine(Path.GetTempPath(), $"cloudsync-executors-{Guid.NewGuid():N}");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(localRoot)) Directory.Delete(localRoot, recursive: true);
    }

    [Fact]
    public async Task download_a_remote_file()
    {
        var provider = new FakeCloudProvider(10);
        provider.Contents[new RemoteItemId("r1")] = "hello"u8.ToArray();
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "docs/a.txt", 5);
        await PlanAsync(host, job);

        await RunAllAsync(host);

        (await File.ReadAllTextAsync(Path.Combine(localRoot, "docs", "a.txt"), Token)).ShouldBe("hello");
        (await Operations(host)).ShouldHaveSingleItem().Status.ShouldBe(OperationStatus.Completed);
        (await KnownAsync(host, job)).ShouldHaveSingleItem().Snapshot.Path.ShouldBe(new RelativePath("docs/a.txt"));
    }

    [Fact]
    public async Task set_the_modified_time_of_a_downloaded_file_to_the_remote_modified_time()
    {
        var provider = new FakeCloudProvider(10);
        provider.Contents[new RemoteItemId("r1")] = "hello"u8.ToArray();
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 5);
        await PlanAsync(host, job);

        await RunAllAsync(host);

        File.GetLastWriteTimeUtc(Path.Combine(localRoot, "a.txt")).ShouldBe(Stamp.UtcDateTime);
    }

    [Fact]
    public async Task replace_a_file_atomically_and_leave_no_temporary_file()
    {
        var provider = new FakeCloudProvider(10);
        provider.Contents[new RemoteItemId("r1")] = "new content"u8.ToArray();
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 11);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item, new KnownSnapshot(new RelativePath("a.txt"), 3, Stamp.AddDays(-1), null, null));
        await File.WriteAllTextAsync(Path.Combine(localRoot, "a.txt"), "old", Token);
        File.SetLastWriteTimeUtc(Path.Combine(localRoot, "a.txt"), Stamp.UtcDateTime.AddDays(-1));
        await RecordLocalAsync(host, job, "a.txt", 3, Stamp.AddDays(-1));
        await PlanAsync(host, job);

        await RunAllAsync(host);

        (await File.ReadAllTextAsync(Path.Combine(localRoot, "a.txt"), Token)).ShouldBe("new content");
        Directory.GetFiles(localRoot).Select(Path.GetFileName).ShouldBe(["a.txt"]);
    }

    [Fact]
    public async Task upload_a_local_file()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        var modified = Stamp.AddHours(3);
        await WriteLocalAsync(host, job, "notes/b.txt", "payload", modified);
        await PlanAsync(host, job);

        await RunAllAsync(host);

        var upload = provider.Uploads.ShouldHaveSingleItem();
        upload.Path.ShouldBe(new RelativePath("notes/b.txt"));
        System.Text.Encoding.UTF8.GetString(upload.Content).ShouldBe("payload");
        upload.ModifiedUtc.ShouldBe(modified);
        (await Operations(host)).ShouldHaveSingleItem().Status.ShouldBe(OperationStatus.Completed);
        (await KnownAsync(host, job)).ShouldHaveSingleItem().Snapshot.Path.ShouldBe(new RelativePath("notes/b.txt"));
    }

    [Fact]
    public async Task dead_letter_an_access_denied_file_and_continue()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await WriteLocalAsync(host, job, "a-locked.txt", "secret", Stamp);
        await WriteLocalAsync(host, job, "b-open.txt", "fine", Stamp);
        await PlanAsync(host, job);
        await using var lockedHandle = new FileStream(Path.Combine(localRoot, "a-locked.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await RunAllAsync(host);

        var operations = await Operations(host);
        operations.Count.ShouldBe(2);
        operations.Count(operation => operation.Status == OperationStatus.DeadLetter).ShouldBe(1);
        operations.Count(operation => operation.Status == OperationStatus.Completed).ShouldBe(1);
        provider.Uploads.ShouldHaveSingleItem().Path.ShouldBe(new RelativePath("b-open.txt"));
        (await KnownAsync(host, job)).ShouldHaveSingleItem().Snapshot.Path.ShouldBe(new RelativePath("b-open.txt"));
    }

    [Fact]
    public async Task rename_a_remote_item_to_the_new_local_path()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "old/a.txt", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item, new KnownSnapshot(new RelativePath("old/a.txt"), 10, Stamp, null, new ContentFingerprint("fp10")));
        await RecordLocalAsync(host, job, "new/a.txt", 10, Stamp);
        await PlanAsync(host, job);

        await RunAllAsync(host);

        provider.Renames.ShouldHaveSingleItem().ShouldBe((new RemoteItemId("r1"), new RelativePath("new/a.txt")));
        (await Operations(host)).ShouldHaveSingleItem().Status.ShouldBe(OperationStatus.Completed);
    }

    [Fact]
    public async Task rename_a_local_file_to_the_new_remote_path_without_overwriting()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "new/a.txt", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item, new KnownSnapshot(new RelativePath("old/a.txt"), 10, Stamp, null, new ContentFingerprint("fp10")));
        Directory.CreateDirectory(Path.Combine(localRoot, "old"));
        await File.WriteAllTextAsync(Path.Combine(localRoot, "old", "a.txt"), "0123456789", Token);
        await RecordLocalAsync(host, job, "old/a.txt", 10, Stamp);
        await PlanAsync(host, job);

        await RunAllAsync(host);

        (await File.ReadAllTextAsync(Path.Combine(localRoot, "new", "a.txt"), Token)).ShouldBe("0123456789");
        File.Exists(Path.Combine(localRoot, "old", "a.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task delete_a_remote_item_the_user_deleted_locally()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item, new KnownSnapshot(new RelativePath("a.txt"), 10, Stamp, null, null));
        await RecordLocalAsync(host, job, "a.txt", 10, Stamp);
        (await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job, [], [new RelativePath("a.txt")], Stamp.AddHours(3), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        await PlanAsync(host, job);

        await RunAllAsync(host);

        provider.Deletes.ShouldHaveSingleItem().ShouldBe(new RemoteItemId("r1"));
        (await KnownAsync(host, job)).ShouldBeEmpty();
    }

    [Fact]
    public async Task keep_a_remote_item_whose_file_is_on_disk_and_dead_letter_the_delete()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item, new KnownSnapshot(new RelativePath("a.txt"), 10, Stamp, null, null));
        await RecordLocalAsync(host, job, "a.txt", 10, Stamp);
        (await host.Services.GetRequiredService<ILocalObservationStore>().RecordAsync(job, [], [new RelativePath("a.txt")], Stamp.AddHours(3), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        await PlanAsync(host, job);
        Directory.CreateDirectory(localRoot);
        await File.WriteAllTextAsync(Path.Combine(localRoot, "a.txt"), "still here", Token);

        await RunAllAsync(host);

        provider.Deletes.ShouldBeEmpty();
        (await Operations(host)).ShouldHaveSingleItem().Status.ShouldBe(OperationStatus.DeadLetter);
    }

    [Fact]
    public async Task delete_a_local_file_the_user_deleted_remotely()
    {
        var provider = new FakeCloudProvider(10);
        await using var host = CreateHost(provider);
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 10);
        var item = (await Items(host, job)).Single().ItemId;
        await CommitKnownAsync(host, job, item, new KnownSnapshot(new RelativePath("a.txt"), 10, Stamp, null, null));
        await File.WriteAllTextAsync(Path.Combine(localRoot, "a.txt"), "0123456789", Token);
        await RecordLocalAsync(host, job, "a.txt", 10, Stamp);
        var deleted = new ProviderChange(ChangeKind.Deleted, new RemoteItemId("r1"));
        (await host.Services.GetRequiredService<IItemIndexStore>().ApplyDeltaPageAsync(new SyncScope(job, Root), [deleted], null, Stamp.AddHours(3), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        await RecordLocalAsync(host, job, "a.txt", 10, Stamp);
        await PlanAsync(host, job);

        await RunAllAsync(host);

        File.Exists(Path.Combine(localRoot, "a.txt")).ShouldBeFalse();
        provider.Deletes.ShouldBeEmpty();
        (await KnownAsync(host, job)).ShouldBeEmpty();
    }

    [Fact]
    public async Task prefer_an_executor_registered_by_the_host()
    {
        var scripted = new ScriptedOperationExecutor(_ => new Ok<Unit>(default));
        await using var host = DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSingleton<ICloudProvider>(new FakeCloudProvider(10));
            services.AddSingleton<IOperationExecutor>(scripted);
            services.AddSyncEngine();
        });
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 5);
        await PlanAsync(host, job);

        await RunAllAsync(host);

        scripted.Executed.Count.ShouldBe(1);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = CreateHost(new FakeCloudProvider(10));
        var job = await SeedAsync(host);
        await AddRemoteAsync(host, job, "r1", "a.txt", 5);
        await PlanAsync(host, job);
        var operation = (await Operations(host)).Single();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IOperationExecutor>().ExecuteAsync(operation, cancelled.Token));
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

    private static async Task AddRemoteAsync(DatabaseTestHost host, SyncJobId job, string id, string path, long size)
    {
        var item = new ProviderItem(new RemoteItemId(id), new RelativePath(path), ItemType.File, size, Stamp);
        var saved = await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(new SyncScope(job, Root), [item], CrawlCheckpoint.None, Stamp, Token);
        saved.Value.ShouldBeOfType<Ok<Unit>>();
    }

    private async Task WriteLocalAsync(DatabaseTestHost host, SyncJobId job, string path, string content, DateTimeOffset modified)
    {
        var full = Path.Combine(localRoot, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content, Token);
        File.SetLastWriteTimeUtc(full, modified.UtcDateTime);
        await RecordLocalAsync(host, job, path, content.Length, modified);
    }

    private static async Task RecordLocalAsync(DatabaseTestHost host, SyncJobId job, string path, long size, DateTimeOffset modified)
    {
        var snapshot = new LocalSnapshot(new RelativePath(path), size, modified, null, new ContentFingerprint("fp" + size));
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

    private static async Task PlanAsync(DatabaseTestHost host, SyncJobId job)
    {
        var planned = await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job, Token);
        planned.Value.ShouldBeOfType<Ok<PlanSummary>>().Value.Queued.ShouldBeGreaterThan(0);
    }

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
