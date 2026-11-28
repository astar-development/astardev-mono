using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class ItemStateStoreShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 28, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_a_file_seen_on_both_sides_with_each_side()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var item = await AddItemAsync(host, job.Id, ItemType.File, "remote-a", "Docs/a.txt", Local(3, "fp-local", "2049:1"), Remote(4, "fp-remote", "etag", "ctag"));

        var listed = await ListAsync(host, job.Id);

        listed.ShouldBe([new StoredItem(item, new RemoteItemId("remote-a"), new RelativePath("Docs/a.txt"), new LocalSnapshot(new RelativePath("Docs/a.txt"), 3, Stamp, new LocalIdentity("2049:1"), new ContentFingerprint("fp-local")), new RemoteSnapshot(new RemoteItemId("remote-a"), new RelativePath("Docs/a.txt"), 4, Stamp, new ETag("etag"), new CTag("ctag"), new ContentFingerprint("fp-remote")), new LocalIdentity("2049:1"), true)]);
    }

    [Fact]
    public async Task list_a_file_seen_only_locally_without_a_remote_side()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var item = await AddItemAsync(host, job.Id, ItemType.File, string.Empty, "a.txt", Local(3, null, null), null);

        var listed = await ListAsync(host, job.Id);

        listed.ShouldBe([new StoredItem(item, new RemoteItemId(string.Empty), new RelativePath("a.txt"), new LocalSnapshot(new RelativePath("a.txt"), 3, Stamp, null, null), null, null, true)]);
    }

    [Fact]
    public async Task list_a_file_seen_only_remotely_as_not_observed_locally()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var item = await AddItemAsync(host, job.Id, ItemType.File, "remote-a", "a.txt", null, Remote(4, null, null, null));

        var listed = await ListAsync(host, job.Id);

        listed.ShouldBe([new StoredItem(item, new RemoteItemId("remote-a"), new RelativePath("a.txt"), null, new RemoteSnapshot(new RemoteItemId("remote-a"), new RelativePath("a.txt"), 4, Stamp, new ETag(string.Empty), null, null), null, false)]);
    }

    [Fact]
    public async Task list_a_locally_deleted_file_without_a_local_side_but_with_its_last_identity()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var item = await AddItemAsync(host, job.Id, ItemType.File, "remote-a", "a.txt", Local(ILocalObservationStore.DeletedSize, null, "2049:1"), Remote(4, null, "etag", null));

        var listed = await ListAsync(host, job.Id);

        listed.ShouldBe([new StoredItem(item, new RemoteItemId("remote-a"), new RelativePath("a.txt"), null, new RemoteSnapshot(new RemoteItemId("remote-a"), new RelativePath("a.txt"), 4, Stamp, new ETag("etag"), null, null), new LocalIdentity("2049:1"), true)]);
    }

    [Fact]
    public async Task leave_out_folders_and_the_files_of_other_jobs()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var other = await AddOtherJobAsync(host);
        var file = await AddItemAsync(host, job.Id, ItemType.File, "remote-a", "a.txt", null, Remote(4, null, null, null));
        await AddItemAsync(host, job.Id, ItemType.Folder, "remote-folder", "Docs", null, Remote(0, null, null, null));
        await AddItemAsync(host, other.Id, ItemType.File, "remote-b", "b.txt", null, Remote(4, null, null, null));

        var listed = await ListAsync(host, job.Id);

        listed.Select(item => item.ItemId).ShouldBe([file]);
    }

    [Fact]
    public async Task list_only_the_requested_files_when_given_item_ids()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var wanted = await AddItemAsync(host, job.Id, ItemType.File, "remote-a", "a.txt", Local(3, null, "2049:1"), Remote(4, null, "etag", null));
        await AddItemAsync(host, job.Id, ItemType.File, "remote-b", "b.txt", Local(3, null, null), Remote(4, null, null, null));

        var listed = await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job.Id, [wanted, new ItemId(Guid.NewGuid())], Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value.ShouldBe((await ListAsync(host, job.Id)).Where(item => item.ItemId == wanted).ToList());
    }

    [Fact]
    public async Task return_nothing_when_given_no_item_ids()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        await AddItemAsync(host, job.Id, ItemType.File, "remote-a", "a.txt", null, Remote(4, null, null, null));

        var listed = await host.Services.GetRequiredService<IItemStateStore>().ListAsync(job.Id, [], Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IItemStateStore>().ListAsync(new SyncJobId(Guid.NewGuid()), cancelled.Token));
    }

    [Fact]
    public async Task honour_cancellation_when_given_item_ids()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IItemStateStore>().ListAsync(new SyncJobId(Guid.NewGuid()), [new ItemId(Guid.NewGuid())], cancelled.Token));
    }

    private static async Task<SyncJob> AddOtherJobAsync(PersistenceTestHost host)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "bob@example.com", "acct-2", DateTimeOffset.UnixEpoch);
        await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token);
        var job = SyncJobStoreShould.NewJob(account.Id, BootstrapMode.Merge);
        await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token);

        return job;
    }

    private static async Task<IReadOnlyList<StoredItem>> ListAsync(PersistenceTestHost host, SyncJobId jobId)
        => (await host.Services.GetRequiredService<IItemStateStore>().ListAsync(jobId, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<StoredItem>>>().Value;

    private static ItemSnapshotEntity Local(long size, string? hash, string? identity)
        => new() { Size = size, ModifiedUtc = Stamp, Hash = hash, LocalIdentity = identity, ObservedUtc = Stamp, SnapshotSource = SnapshotSource.Local };

    private static ItemSnapshotEntity Remote(long size, string? hash, string? etag, string? ctag)
        => new() { Size = size, ModifiedUtc = Stamp, Hash = hash, ETag = etag is null ? null : new ETag(etag), CTag = ctag, ObservedUtc = Stamp, SnapshotSource = SnapshotSource.Remote };

    private static async Task<ItemId> AddItemAsync(PersistenceTestHost host, SyncJobId jobId, ItemType type, string remoteId, string path, ItemSnapshotEntity? local, ItemSnapshotEntity? remote)
    {
        var id = new ItemId(Guid.NewGuid());
        await using var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(Token);
        context.Items.Add(new ItemEntity { Id = id, SyncJobId = jobId, ItemType = type, RemoteItemId = new RemoteItemId(remoteId), LocalPath = new RelativePath(path), CreatedUtc = Stamp, UpdatedUtc = Stamp });
        foreach (var snapshot in new[] { local, remote }.OfType<ItemSnapshotEntity>())
        {
            snapshot.Id = new SnapshotId(Guid.NewGuid());
            snapshot.ItemId = id;
            context.ItemSnapshots.Add(snapshot);
        }

        await context.SaveChangesAsync(Token);

        return id;
    }
}
