using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Turns Graph drive items into <see cref="ProviderItem"/> so no Graph type crosses the provider boundary.</summary>
internal static class DriveItemMapping
{
    private const string RootMarker = "root:";

    /// <summary>Maps one drive item. The path is the parent path below the drive root joined with the item name, without a leading slash.</summary>
    public static ProviderItem ToProviderItem(GraphDriveItem item) => new(
        new RemoteItemId(item.Id),
        new RelativePath(RelativeTo(item.ParentReference?.Path, item.Name)),
        item.Folder is null ? ItemType.File : ItemType.Folder,
        item.Size,
        item.LastModifiedDateTime);

    /// <summary>Maps one drive item whose sync-root-relative path is already worked out.</summary>
    public static ProviderItem ToProviderItem(GraphDriveItem item, string path) => new(
        new RemoteItemId(item.Id),
        new RelativePath(path),
        item.Folder is null ? ItemType.File : ItemType.Folder,
        item.Size,
        item.LastModifiedDateTime);

    private static string RelativeTo(string? parentPath, string name)
    {
        var markerAt = parentPath?.IndexOf(RootMarker, StringComparison.Ordinal) ?? -1;
        var parent = markerAt < 0 ? string.Empty : parentPath![(markerAt + RootMarker.Length)..].Trim('/');

        return parent.Length == 0 ? name : $"{parent}/{name}";
    }
}
