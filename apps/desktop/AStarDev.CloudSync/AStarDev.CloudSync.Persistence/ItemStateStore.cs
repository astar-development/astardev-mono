using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>SQLite implementation of <see cref="IItemStateStore"/>. It reads each file with its one snapshot per side in a single query: the item left-joined to its <c>Local</c> and its <c>Remote</c> snapshot.</summary>
internal sealed class ItemStateStore(IDbContextFactory<CloudSyncDbContext> contextFactory) : IItemStateStore
{
    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<StoredItem>, StoreError>> ListAsync(SyncJobId jobId, CancellationToken cancellationToken)
        => ListWhereAsync(context => context.Items.Where(row => row.SyncJobId == jobId && row.ItemType == ItemType.File), cancellationToken);

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<StoredItem>, StoreError>> ListAsync(SyncJobId jobId, IReadOnlyCollection<ItemId> itemIds, CancellationToken cancellationToken)
        => ListWhereAsync(context => context.Items.Where(row => row.SyncJobId == jobId && row.ItemType == ItemType.File && EF.Parameter(itemIds).Contains(row.Id)), cancellationToken);

    private async Task<Result<IReadOnlyList<StoredItem>, StoreError>> ListWhereAsync(Func<CloudSyncDbContext, IQueryable<ItemEntity>> files, CancellationToken cancellationToken)
    {
        var outcome = await Try.RunAsync<IReadOnlyList<StoredItem>>(async token =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(token).ConfigureAwait(false);
            var rows = await (from item in files(context).AsNoTracking()
                              join localRow in Side(context, SnapshotSource.Local) on item.Id equals localRow.ItemId into locals
                              from local in locals.DefaultIfEmpty()
                              join remoteRow in Side(context, SnapshotSource.Remote) on item.Id equals remoteRow.ItemId into remotes
                              from remote in remotes.DefaultIfEmpty()
                              select new { item, local, remote }).ToListAsync(token).ConfigureAwait(false);

            return rows.Select(row => ToStoredItem(row.item, row.local, row.remote)).ToList();
        }, cancellationToken).ConfigureAwait(false);

        return outcome.ToResult(exception => (StoreError)new StorageFailed(exception.Message));
    }

    private static IQueryable<ItemSnapshotEntity> Side(CloudSyncDbContext context, SnapshotSource source) => context.ItemSnapshots.AsNoTracking().Where(snapshot => snapshot.SnapshotSource == source);

    private static StoredItem ToStoredItem(ItemEntity item, ItemSnapshotEntity? seen, ItemSnapshotEntity? reported)
    {
        var local = seen is not null && seen.Size != ILocalObservationStore.DeletedSize
            ? new LocalSnapshot(item.LocalPath, seen.Size, seen.ModifiedUtc, ToIdentity(seen), seen.Hash is { } hash ? new ContentFingerprint(hash) : null)
            : null;
        var remote = reported is not null
            ? new RemoteSnapshot(item.RemoteItemId, item.LocalPath, reported.Size, reported.ModifiedUtc, reported.ETag ?? new ETag(string.Empty), reported.CTag is { } tag ? new CTag(tag) : null, reported.Hash is { } digest ? new ContentFingerprint(digest) : null)
            : null;

        return new StoredItem(item.Id, item.RemoteItemId, item.LocalPath, local, remote, seen is null ? null : ToIdentity(seen), seen is not null);
    }

    private static LocalIdentity? ToIdentity(ItemSnapshotEntity snapshot) => snapshot.LocalIdentity is { } text ? new LocalIdentity(text) : null;
}
