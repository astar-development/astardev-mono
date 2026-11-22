namespace AStarDev.CloudSync.OneDrive;

/// <summary>Where a drive item's parent sits.</summary>
/// <param name="Path">Graph path such as <c>/drive/root:/Docs</c>. <c>null</c> when Graph omits it.</param>
/// <param name="Id">Graph id of the parent folder. <c>null</c> for the drive root.</param>
internal sealed record GraphParentReference(string? Path, string? Id = null);
