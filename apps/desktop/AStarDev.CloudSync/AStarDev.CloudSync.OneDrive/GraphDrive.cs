namespace AStarDev.CloudSync.OneDrive;

/// <summary>The part of a Graph drive the provider reads.</summary>
/// <param name="Quota">Storage figures. Absent when Graph reports none.</param>
internal sealed record GraphDrive(GraphQuota? Quota);
