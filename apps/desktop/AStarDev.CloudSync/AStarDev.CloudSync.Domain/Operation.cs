namespace AStarDev.CloudSync.Domain;

/// <summary>One durable unit of work against one item. It is stored before it runs, so it survives a restart.</summary>
/// <param name="Id">Our identity for the operation.</param>
/// <param name="SyncJobId">The job the operation belongs to.</param>
/// <param name="ItemId">The item acted on. Two operations for one item never run at once.</param>
/// <param name="Type">What the operation does.</param>
/// <param name="Priority">Decides claim order before age.</param>
/// <param name="Status">Where the operation is in its life.</param>
/// <param name="Attempts">How many times the operation has been claimed.</param>
/// <param name="NextAttemptUtc">The operation is not claimed before this time, which delays a retry.</param>
/// <param name="LastError">Why the last attempt failed; <see langword="null"/> when none has.</param>
/// <param name="CreatedUtc">When the operation was enqueued. Older operations are claimed first within a priority.</param>
public sealed record Operation(OperationId Id, SyncJobId SyncJobId, ItemId ItemId, OperationType Type, OperationPriority Priority, OperationStatus Status, int Attempts, DateTimeOffset NextAttemptUtc, string? LastError, DateTimeOffset CreatedUtc);
