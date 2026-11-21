using AStarDev.CloudSync.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class EntityMappingShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task round_trip_strongly_typed_ids()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var factory = host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>();
        var accountId = new AccountId(Guid.NewGuid());
        var syncJobId = new SyncJobId(Guid.NewGuid());
        var parentId = new ItemId(Guid.NewGuid());
        var childId = new ItemId(Guid.NewGuid());
        var snapshotId = new SnapshotId(Guid.NewGuid());
        var created = new DateTimeOffset(2026, 11, 21, 9, 30, 0, TimeSpan.Zero);

        await using (var writer = await factory.CreateDbContextAsync(Token))
        {
            writer.Accounts.Add(new AccountEntity { Id = accountId, ProviderType = "OneDrive", DisplayName = "Personal", AccountIdentifier = "acct-1", CreatedUtc = created });
            writer.SyncJobs.Add(new SyncJobEntity { Id = syncJobId, AccountId = accountId, Name = "Docs", LocalRootPath = "/home/u/OneDrive", RemoteRootId = new RemoteItemId("root"), Enabled = true, CreatedUtc = created, UpdatedUtc = created });
            writer.Items.Add(new ItemEntity { Id = parentId, SyncJobId = syncJobId, ItemType = ItemType.Folder, ParentItemId = null, RemoteItemId = new RemoteItemId("remote-parent"), LocalPath = new RelativePath("Docs"), CreatedUtc = created, UpdatedUtc = created });
            writer.Items.Add(new ItemEntity { Id = childId, SyncJobId = syncJobId, ItemType = ItemType.File, ParentItemId = parentId, RemoteItemId = new RemoteItemId("remote-child"), LocalPath = new RelativePath("Docs/a.txt"), CreatedUtc = created, UpdatedUtc = created });
            writer.ItemSnapshots.Add(new ItemSnapshotEntity { Id = snapshotId, ItemId = childId, Size = 42, ModifiedUtc = created, Hash = "abc", ETag = new ETag("etag-1"), CTag = "ctag-1", ObservedUtc = created, SnapshotSource = SnapshotSource.Remote });
            writer.ProviderStates.Add(new ProviderStateEntity { SyncJobId = syncJobId, DeltaToken = new DeltaToken("delta-1"), LastSyncUtc = created, LastScanUtc = created });
            await writer.SaveChangesAsync(Token);
        }

        await using var reader = await factory.CreateDbContextAsync(Token);
        var account = await reader.Accounts.SingleAsync(Token);
        var job = await reader.SyncJobs.SingleAsync(Token);
        var items = await reader.Items.OrderBy(item => item.LocalPath).ToListAsync(Token);
        var snapshot = await reader.ItemSnapshots.SingleAsync(Token);
        var state = await reader.ProviderStates.SingleAsync(Token);

        account.Id.ShouldBe(accountId);
        job.Id.ShouldBe(syncJobId);
        job.AccountId.ShouldBe(accountId);
        job.RemoteRootId.ShouldBe(new RemoteItemId("root"));
        items.Select(item => item.Id).ShouldBe([parentId, childId]);
        items[0].ParentItemId.ShouldBeNull();
        items[1].ParentItemId.ShouldBe(parentId);
        items[1].RemoteItemId.ShouldBe(new RemoteItemId("remote-child"));
        items[1].LocalPath.ShouldBe(new RelativePath("Docs/a.txt"));
        items[1].ItemType.ShouldBe(ItemType.File);
        snapshot.Id.ShouldBe(snapshotId);
        snapshot.ItemId.ShouldBe(childId);
        snapshot.ETag.ShouldBe(new ETag("etag-1"));
        snapshot.SnapshotSource.ShouldBe(SnapshotSource.Remote);
        state.SyncJobId.ShouldBe(syncJobId);
        state.DeltaToken.ShouldBe(new DeltaToken("delta-1"));
        account.CreatedUtc.ShouldBe(created);
    }
}
