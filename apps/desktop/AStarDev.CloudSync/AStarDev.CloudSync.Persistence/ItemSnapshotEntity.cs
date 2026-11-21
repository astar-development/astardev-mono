using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored form of one observation of an item at a point in time.</summary>
internal sealed class ItemSnapshotEntity
{
    /// <summary>Primary key.</summary>
    public SnapshotId Id { get; set; }

    /// <summary>The observed item; deleting the item deletes its snapshots.</summary>
    public ItemId ItemId { get; set; }

    /// <summary>Observed size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>The last-modified time observed.</summary>
    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary><see langword="null"/> when no hash was observed.</summary>
    public string? Hash { get; set; }

    /// <summary>The provider's version marker; <see langword="null"/> for a local observation.</summary>
    public ETag? ETag { get; set; }

    /// <summary>The provider's metadata version marker; <see langword="null"/> for a local observation.</summary>
    public string? CTag { get; set; }

    /// <summary>When the observation was taken.</summary>
    public DateTimeOffset ObservedUtc { get; set; }

    /// <summary>Which side of the sync the observation came from.</summary>
    public SnapshotSource SnapshotSource { get; set; }
}
