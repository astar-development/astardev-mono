using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored last reconciled state of one item. The item is not a foreign key, so the baseline outlives the item row that a remote delete removes; it goes when its job goes.</summary>
internal sealed class KnownSnapshotEntity
{
    /// <summary>Primary key: the item the baseline belongs to.</summary>
    public ItemId ItemId { get; set; }

    /// <summary>The job the item belongs to; deleting the job deletes its baselines.</summary>
    public SyncJobId SyncJobId { get; set; }

    /// <summary>Where the item sat on both sides at that point.</summary>
    public RelativePath Path { get; set; }

    /// <summary>Content size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Last-modified time of the content.</summary>
    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary>The provider's content version; <see langword="null"/> when none was recorded.</summary>
    public ETag? ETag { get; set; }

    /// <summary>Content digest text; <see langword="null"/> when none was computed.</summary>
    public string? Hash { get; set; }

    /// <summary>Copies a domain baseline into its stored form.</summary>
    /// <param name="jobId">The job the item belongs to.</param>
    /// <param name="itemId">The item the baseline belongs to.</param>
    /// <param name="snapshot">The baseline to store.</param>
    /// <returns>A new, untracked entity.</returns>
    public static KnownSnapshotEntity From(SyncJobId jobId, ItemId itemId, KnownSnapshot snapshot)
        => new() { ItemId = itemId, SyncJobId = jobId, Path = snapshot.Path, Size = snapshot.Size, ModifiedUtc = snapshot.ModifiedUtc, ETag = snapshot.ETag, Hash = snapshot.Fingerprint?.Value };

    /// <summary>Builds the domain baseline from the stored row.</summary>
    /// <returns>An immutable baseline with the same values.</returns>
    public KnownSnapshot ToSnapshot() => new(Path, Size, ModifiedUtc, ETag, Hash is { } hash ? new ContentFingerprint(hash) : null);
}
