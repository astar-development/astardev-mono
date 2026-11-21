namespace AStarDev.CloudSync.OneDrive;

/// <summary>Where a drive item's parent sits.</summary>
/// <param name="Path">Graph path such as <c>/drive/root:/Docs</c>. <c>null</c> when Graph omits it.</param>
internal sealed record GraphParentReference(string? Path);
