namespace AStarDev.CloudSync.OneDrive;

/// <summary>The parts of a Graph drive item the provider reads.</summary>
/// <param name="Id">Graph's stable item id.</param>
/// <param name="Name">The item's own name, without its parent path.</param>
/// <param name="Size">Content size in bytes.</param>
/// <param name="LastModifiedDateTime">When Graph last recorded a change.</param>
/// <param name="Folder">Present only for folders.</param>
/// <param name="ParentReference">Where the item sits. Its path is absent for the drive root itself.</param>
internal sealed record GraphDriveItem(string Id, string Name, long Size, DateTimeOffset LastModifiedDateTime, GraphFolderFacet? Folder, GraphParentReference? ParentReference);
