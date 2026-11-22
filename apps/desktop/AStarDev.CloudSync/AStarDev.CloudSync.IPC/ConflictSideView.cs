namespace AStarDev.CloudSync.IPC;

/// <summary>What one side, the local disk or the cloud, holds for a conflicted item, as the Conflict Centre compares them.</summary>
/// <param name="Path">Where the item sits on that side, relative to the synchronised root. It differs between the sides for a rename conflict.</param>
/// <param name="ModifiedUtc">When that side's content last changed.</param>
/// <param name="SizeBytes">That side's content size in bytes.</param>
public sealed record ConflictSideView(string Path, DateTimeOffset ModifiedUtc, long SizeBytes);
