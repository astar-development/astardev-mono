using AStarDev.CloudSync.Domain;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Keeps one <see cref="ItemSnapshotEntity"/> per item and side: a new observation overwrites the previous one, and an unchanged observation writes nothing, so the table grows with items, not with scans.</summary>
internal static class ItemSnapshotRows
{
    /// <summary>Loads the tracked snapshot on <paramref name="source"/> of each of <paramref name="itemIds"/>, keyed by item; an item never observed on that side has no entry.</summary>
    public static Task<Dictionary<ItemId, ItemSnapshotEntity>> LoadAsync(CloudSyncDbContext context, IReadOnlyCollection<ItemId> itemIds, SnapshotSource source, CancellationToken cancellationToken)
        => context.ItemSnapshots.Where(row => row.SnapshotSource == source && itemIds.Contains(row.ItemId)).ToDictionaryAsync(row => row.ItemId, cancellationToken);

    /// <summary>Records an observation of <paramref name="itemId"/>: adds its row when none exists, otherwise overwrites the row only when size, modified time, hash or identity differ, so <see cref="ItemSnapshotEntity.ObservedUtc"/> is when the item was last seen to change.</summary>
    public static void Observe(CloudSyncDbContext context, Dictionary<ItemId, ItemSnapshotEntity> snapshots, ItemId itemId, SnapshotSource source, long size, DateTimeOffset modifiedUtc, string? hash, string? identity, DateTimeOffset observedUtc)
    {
        if (!snapshots.TryGetValue(itemId, out var row))
        {
            row = new ItemSnapshotEntity { Id = new SnapshotId(Guid.NewGuid()), ItemId = itemId, SnapshotSource = source };
            snapshots[itemId] = row;
            context.ItemSnapshots.Add(row);
        }
        else if (IsUnchanged(row, size, modifiedUtc, hash, identity)) return;

        row.Size = size;
        row.ModifiedUtc = modifiedUtc;
        row.Hash = hash;
        row.LocalIdentity = identity;
        row.ObservedUtc = observedUtc;
    }

    private static bool IsUnchanged(ItemSnapshotEntity row, long size, DateTimeOffset modifiedUtc, string? hash, string? identity)
        => row.Size == size && row.ModifiedUtc == modifiedUtc && row.Hash == hash && row.LocalIdentity == identity;
}
