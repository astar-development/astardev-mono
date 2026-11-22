using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored form of a queued operation. The item is not a foreign key, so an operation outlives a re-crawl that replaces item rows; it goes when its job goes.</summary>
internal sealed class OperationEntity
{
    /// <summary>Primary key.</summary>
    public OperationId Id { get; set; }

    /// <summary>The job the operation belongs to; deleting the job deletes its operations.</summary>
    public SyncJobId SyncJobId { get; set; }

    /// <summary>The item acted on; the unit of locking.</summary>
    public ItemId ItemId { get; set; }

    /// <summary>What the operation does; stored as the enum name.</summary>
    public OperationType Type { get; set; }

    /// <summary>Claim order before age; stored as the number so it sorts in declaration order.</summary>
    public OperationPriority Priority { get; set; }

    /// <summary>Where the operation is in its life; stored as the enum name.</summary>
    public OperationStatus Status { get; set; }

    /// <summary>How many times the operation has been claimed.</summary>
    public int Attempts { get; set; }

    /// <summary>Not claimed before this time; always stored in UTC so stored values compare by time.</summary>
    public DateTimeOffset NextAttemptUtc { get; set; }

    /// <summary>Why the last attempt failed; <see langword="null"/> when none has.</summary>
    public string? LastError { get; set; }

    /// <summary>When the operation was enqueued; always stored in UTC so stored values compare by time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>The W3C <c>traceparent</c> of the planning span; <see langword="null"/> when none was recorded.</summary>
    public string? TraceParent { get; set; }

    /// <summary>Copies the domain operation into its stored form, converting times to UTC.</summary>
    /// <param name="operation">The operation to store.</param>
    /// <returns>A new, untracked entity.</returns>
    public static OperationEntity From(Operation operation)
        => new() { Id = operation.Id, SyncJobId = operation.SyncJobId, ItemId = operation.ItemId, Type = operation.Type, Priority = operation.Priority, Status = operation.Status, Attempts = operation.Attempts, NextAttemptUtc = operation.NextAttemptUtc.ToUniversalTime(), LastError = operation.LastError, CreatedUtc = operation.CreatedUtc.ToUniversalTime(), TraceParent = operation.TraceParent };

    /// <summary>Builds the domain operation from the stored row.</summary>
    /// <returns>An immutable operation with the same values.</returns>
    public Operation ToOperation() => new(Id, SyncJobId, ItemId, Type, Priority, Status, Attempts, NextAttemptUtc, LastError, CreatedUtc, TraceParent);
}
