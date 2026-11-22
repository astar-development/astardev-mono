using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>A proven rename: the known item now sits at a new path on one side. Feed the new snapshot to the reconciler as that side's current state.</summary>
/// <param name="ItemId">Our identity for the renamed item.</param>
/// <param name="To">The path of the created item that the known item became.</param>
internal sealed record DetectedRename(ItemId ItemId, RelativePath To);
