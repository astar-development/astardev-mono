namespace AStarDev.CloudSync.OneDrive;

/// <summary>Marks a drive item in a delta response as removed. Only its presence matters.</summary>
/// <param name="State">Graph's reason text, when it sends one.</param>
internal sealed record GraphDeletedFacet(string? State);
