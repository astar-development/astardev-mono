using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>One operation the planner wants queued for an item.</summary>
/// <param name="Type">What the operation does.</param>
/// <param name="Priority">How soon it is claimed. Renames outrank content work so a rename always runs before the update of the same item.</param>
internal sealed record PlannedOperation(OperationType Type, OperationPriority Priority);
