using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The baseline an item takes once one operation succeeds. Written before the operation is queued and removed when the operation commits.</summary>
internal sealed class StagedKnownEntity
{
    /// <summary>Primary key: the operation whose success applies the change.</summary>
    public OperationId OperationId { get; set; }

    /// <summary>The job the operation belongs to; deleting the job deletes the staged change.</summary>
    public SyncJobId SyncJobId { get; set; }

    /// <summary>The item whose baseline changes.</summary>
    public ItemId ItemId { get; set; }

    /// <summary><see langword="true"/> when the baseline is removed instead of replaced; the other values are then unused.</summary>
    public bool Remove { get; set; }

    /// <summary>The new baseline path.</summary>
    public RelativePath Path { get; set; }

    /// <summary>The new baseline size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>The new baseline modified time.</summary>
    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary>The new baseline content version; <see langword="null"/> when none.</summary>
    public ETag? ETag { get; set; }

    /// <summary>The new baseline digest text; <see langword="null"/> when none.</summary>
    public string? Hash { get; set; }
}
