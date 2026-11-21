using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored identity bridge between the database, the filesystem and the provider for one file or folder.</summary>
internal sealed class ItemEntity
{
    /// <summary>Primary key; stays the same when the item is renamed or moved.</summary>
    public ItemId Id { get; set; }

    /// <summary>The job the item belongs to; deleting the job deletes its items.</summary>
    public SyncJobId SyncJobId { get; set; }

    /// <summary>Whether the item is a file or a folder.</summary>
    public ItemType ItemType { get; set; }

    /// <summary><see langword="null"/> for the root of a job; otherwise the containing folder, which cannot be deleted while it has children.</summary>
    public ItemId? ParentItemId { get; set; }

    /// <summary>The provider's stable id for the item.</summary>
    public RemoteItemId RemoteItemId { get; set; }

    /// <summary>The item's current location under the job's local root.</summary>
    public RelativePath LocalPath { get; set; }

    /// <summary>When the item was first seen.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>When the item was last changed.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
