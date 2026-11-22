using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IItemStateStore"/>. It reads the whole job in two queries and keeps only the latest snapshot per side.</summary>
internal sealed class ItemStateStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IItemStateStore
{
    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<StoredItem>, StoreError>> ListAsync(SyncJobId jobId, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<IReadOnlyList<StoredItem>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var items = await context.Items.AsNoTracking().Where(row => row.SyncJobId == jobId && row.ItemType == ItemType.File).ToListAsync(token).ConfigureAwait(false);
            var snapshots = (await (from snapshot in context.ItemSnapshots.AsNoTracking()
                                    join item in context.Items.AsNoTracking() on snapshot.ItemId equals item.Id
                                    where item.SyncJobId == jobId && item.ItemType == ItemType.File
                                    select snapshot).ToListAsync(token).ConfigureAwait(false))
                .GroupBy(snapshot => (snapshot.ItemId, snapshot.SnapshotSource))
                .ToDictionary(group => group.Key, group => group.MaxBy(snapshot => snapshot.ObservedUtc)!);

            return items.Select(item => ToStoredItem(item, snapshots)).ToList();
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(exception => (StoreError)new StorageFailed(exception.Message));
    }

    private static StoredItem ToStoredItem(ItemEntity item, Dictionary<(ItemId, SnapshotSource), ItemSnapshotEntity> snapshots)
    {
        var local = snapshots.TryGetValue((item.Id, SnapshotSource.Local), out var seen) && seen.Size != ILocalObservationStore.DeletedSize
            ? new LocalSnapshot(item.LocalPath, seen.Size, seen.ModifiedUtc, ToIdentity(seen), seen.Hash is { } hash ? new ContentFingerprint(hash) : null)
            : null;
        var remote = snapshots.TryGetValue((item.Id, SnapshotSource.Remote), out var reported)
            ? new RemoteSnapshot(item.RemoteItemId, item.LocalPath, reported.Size, reported.ModifiedUtc, reported.ETag ?? new ETag(string.Empty), reported.CTag is { } tag ? new CTag(tag) : null, reported.Hash is { } digest ? new ContentFingerprint(digest) : null)
            : null;

        return new StoredItem(item.Id, item.RemoteItemId, item.LocalPath, local, remote, snapshots.TryGetValue((item.Id, SnapshotSource.Local), out var last) ? ToIdentity(last) : null);
    }

    private static LocalIdentity? ToIdentity(ItemSnapshotEntity snapshot) => snapshot.LocalIdentity is { } text ? new LocalIdentity(text) : null;
}
