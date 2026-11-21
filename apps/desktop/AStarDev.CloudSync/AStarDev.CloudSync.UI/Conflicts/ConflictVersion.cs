namespace AStarDev.CloudSync.UI.Conflicts;

/// <summary>One side of a conflict.</summary>
/// <param name="Path">Where this version lives, local or remote.</param>
/// <param name="Modified">When this version last changed.</param>
/// <param name="SizeBytes">Size in bytes; zero for a version that no longer exists.</param>
internal sealed record ConflictVersion(string Path, DateTimeOffset Modified, long SizeBytes);
