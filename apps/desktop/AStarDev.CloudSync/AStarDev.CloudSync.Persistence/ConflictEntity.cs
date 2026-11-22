using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored form of a conflict. The item is not a foreign key, so a conflict outlives a re-crawl that replaces item rows; it goes when its job goes.</summary>
internal sealed class ConflictEntity
{
    /// <summary>Primary key.</summary>
    public ConflictId Id { get; set; }

    /// <summary>The job the conflict belongs to; deleting the job deletes its conflicts.</summary>
    public SyncJobId SyncJobId { get; set; }

    /// <summary>The item in dispute.</summary>
    public ItemId ItemId { get; set; }

    /// <summary>The competing intents; stored as the enum name.</summary>
    public ConflictType Type { get; set; }

    /// <summary>Where the conflict is in its life; stored as the enum name.</summary>
    public ConflictStatus Status { get; set; }

    /// <summary>When the conflict was detected; always stored in UTC so stored values compare by time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Copies the domain conflict into its stored form, converting the time to UTC.</summary>
    /// <param name="conflict">The conflict to store.</param>
    /// <returns>A new, untracked entity.</returns>
    public static ConflictEntity From(Conflict conflict)
        => new() { Id = conflict.Id, SyncJobId = conflict.SyncJobId, ItemId = conflict.ItemId, Type = conflict.Type, Status = conflict.Status, CreatedUtc = conflict.CreatedUtc.ToUniversalTime() };

    /// <summary>Builds the domain conflict from the stored row.</summary>
    /// <returns>An immutable conflict with the same values.</returns>
    public Conflict ToConflict() => new(Id, SyncJobId, ItemId, Type, Status, CreatedUtc);
}
