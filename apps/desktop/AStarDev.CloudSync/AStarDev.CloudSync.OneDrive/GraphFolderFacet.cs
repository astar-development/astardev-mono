namespace AStarDev.CloudSync.OneDrive;

/// <summary>Marks a drive item as a folder. Only its presence matters.</summary>
/// <param name="ChildCount">Number of children Graph reports, when it does.</param>
internal sealed record GraphFolderFacet(int? ChildCount);
