using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>A known item that is missing from one side and may have been renamed rather than deleted.</summary>
/// <param name="ItemId">Our identity for the item.</param>
/// <param name="Evidence">What was known about the item at its baseline path, including its identity on that side when one was recorded.</param>
internal sealed record DeletedItem(ItemId ItemId, RenameEvidence Evidence);
