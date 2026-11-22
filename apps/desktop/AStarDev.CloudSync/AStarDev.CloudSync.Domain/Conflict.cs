namespace AStarDev.CloudSync.Domain;

/// <summary>An unresolved disagreement about one item. It stays until the user resolves it.</summary>
/// <param name="Id">Our identity for the conflict.</param>
/// <param name="SyncJobId">The job the item belongs to.</param>
/// <param name="ItemId">The item in dispute.</param>
/// <param name="Type">The competing intents.</param>
/// <param name="Status">Where the conflict is in its life.</param>
/// <param name="CreatedUtc">When the conflict was detected.</param>
public sealed record Conflict(ConflictId Id, SyncJobId SyncJobId, ItemId ItemId, ConflictType Type, ConflictStatus Status, DateTimeOffset CreatedUtc);
