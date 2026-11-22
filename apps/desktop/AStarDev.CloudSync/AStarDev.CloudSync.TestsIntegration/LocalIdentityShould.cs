using System.Text;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

[Collection("Operation scheduler metrics")]
public sealed class LocalIdentityShould : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
    private static readonly RemoteItemId Root = new("root");
    private readonly string root = Directory.CreateTempSubdirectory("cloudsync-identity-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public async Task detect_a_local_rename_by_identity()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Filesystem identity is read on Linux only.");
        await using var host = CreateHost();
        var (job, item) = await SyncedFileAsync(host, "a.txt", "hello");

        File.Move(Path.Combine(root, "a.txt"), Path.Combine(root, "b.txt"));
        await ScanAsync(host, job);

        var state = await StateOfAsync(host, job, item);
        state.Local.ShouldNotBeNull().Path.ShouldBe(new RelativePath("b.txt"));
    }

    [Fact]
    public async Task detect_a_move_to_another_folder()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Filesystem identity is read on Linux only.");
        await using var host = CreateHost();
        var (job, item) = await SyncedFileAsync(host, "a.txt", "hello");

        Directory.CreateDirectory(Path.Combine(root, "archive"));
        File.Move(Path.Combine(root, "a.txt"), Path.Combine(root, "archive", "a.txt"));
        await ScanAsync(host, job);

        var state = await StateOfAsync(host, job, item);
        state.Local.ShouldNotBeNull().Path.ShouldBe(new RelativePath("archive/a.txt"));
    }

    [Fact]
    public async Task not_treat_a_reused_inode_with_different_content_as_a_rename()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Filesystem identity is read on Linux only.");
        await using var host = CreateHost();
        var (job, item) = await SyncedFileAsync(host, "a.txt", "hello");

        File.Move(Path.Combine(root, "a.txt"), Path.Combine(root, "b.txt"));
        Write("b.txt", "completely different and longer content");
        await ScanAsync(host, job);

        var state = await StateOfAsync(host, job, item);
        state.Local.ShouldBeNull();
    }

    [Fact]
    public async Task queue_a_remote_rename_instead_of_delete_and_upload()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Filesystem identity is read on Linux only.");
        await using var host = CreateHost();
        var (job, _) = await SyncedFileAsync(host, "a.txt", "hello");

        File.Move(Path.Combine(root, "a.txt"), Path.Combine(root, "b.txt"));
        await ScanAsync(host, job);
        var summary = (await host.Services.GetRequiredService<SyncPlanner>().PlanAsync(job.Id, Token)).Value.ShouldBeOfType<Ok<PlanSummary>>().Value;

        summary.ShouldBe(new PlanSummary(1, 0));
        var operation = (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value.ShouldHaveSingleItem();
        operation.Type.ShouldBe(OperationType.RenameRemote);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = CreateHost();
        var (job, _) = await SyncedFileAsync(host, "a.txt", "hello");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<LocalObserver>().ScanAsync(job, default, cancelled.Token));
    }

    private static DatabaseTestHost CreateHost()
        => DatabaseTestHost.Create(services =>
        {
            services.AddSingleton<TimeProvider>(new InstantTimeProvider());
            services.AddSingleton<IOperationExecutor>(new ScriptedOperationExecutor(_ => new Ok<Unit>(default)));
            services.AddSyncEngine();
        });

    private void Write(string relative, string content) => File.WriteAllText(Path.Combine(root, relative), content, Encoding.ASCII);

    private static async Task ScanAsync(DatabaseTestHost host, SyncJob job)
        => (await host.Services.GetRequiredService<LocalObserver>().ScanAsync(job, default, Token)).Value.ShouldBeOfType<Ok<Unit>>();

    private async Task<(SyncJob Job, ItemId Item)> SyncedFileAsync(DatabaseTestHost host, string path, string content)
    {
        Write(path, content);
        File.SetLastWriteTimeUtc(Path.Combine(root, path), Stamp.UtcDateTime);
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Stamp);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", root, Root, BootstrapMode.Merge, true, Stamp, Stamp);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(new SyncScope(job.Id, Root), Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var remote = new ProviderItem(new RemoteItemId("r1"), new RelativePath(path), ItemType.File, content.Length, Stamp);
        (await host.Services.GetRequiredService<IItemIndexStore>().SavePageAsync(new SyncScope(job.Id, Root), [remote], CrawlCheckpoint.None, Stamp, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        await ScanAsync(host, job);
        var present = (await host.Services.GetRequiredService<ILocalObservationStore>().ListPresentAsync(job.Id, default, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<LocalSnapshot>>>().Value.ShouldHaveSingleItem();
        var item = (await Items(host, job.Id)).Single(row => row.RemoteItemId.Value.Length > 0).ItemId;
        var known = new KnownSnapshot(new RelativePath(path), content.Length, Stamp, null, present.Fingerprint);
        var store = host.Services.GetRequiredService<IKnownSnapshotStore>();
        var operation = new OperationId(Guid.NewGuid());
        (await store.StageAsync(job.Id, operation, item, known, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await store.CommitAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return (job, item);
    }

    private static async Task<IReadOnlyList<StoredItem>> Items(DatabaseTestHost host, SyncJobId job)
        => (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value;

    private static async Task<CanonicalItemState> StateOfAsync(DatabaseTestHost host, SyncJob job, ItemId item)
    {
        var items = await Items(host, job.Id);
        var known = (await host.Services.GetRequiredService<IKnownSnapshotStore>().ListAsync(job.Id, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<KnownEntry>>>().Value;

        return CanonicalStateBuilder.Build(items, known).Single(state => state.ItemId == item);
    }
}
