namespace AStarDev.CloudSync.Domain;

/// <summary>Our own identity for one observation of an item at a point in time. An item has many over its life.</summary>
/// <param name="Value">Unique per observation.</param>
public readonly record struct SnapshotId(Guid Value);
