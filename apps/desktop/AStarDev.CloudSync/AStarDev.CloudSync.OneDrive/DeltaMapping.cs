using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Turns a Graph delta page into a <see cref="ProviderDeltaPage"/> so no Graph type crosses the provider boundary.</summary>
internal static class DeltaMapping
{
    /// <summary>Maps one page. An item without an entry in <paramref name="paths"/> is left out. The resume token is the next link and the delta token the delta link; each is <see langword="null"/> when Graph sent none.</summary>
    public static ProviderDeltaPage ToDeltaPage(GraphChildrenPage page, IReadOnlyDictionary<string, string> paths) => new(
        (page.Value ?? []).Where(item => item.Deleted is not null || paths.ContainsKey(item.Id)).Select(item => ToChange(item, paths)).ToList(),
        page.NextLink,
        page.DeltaLink is { } link ? new DeltaToken(link) : null);

    /// <summary>Maps one page of a full enumeration. Deleted items, the root folder itself and items without an entry in <paramref name="paths"/> are left out; the resume token is the next link and the delta token the delta link, each <see langword="null"/> when Graph sent none.</summary>
    public static ProviderSnapshotPage ToSnapshotPage(RemoteItemId root, GraphChildrenPage page, IReadOnlyDictionary<string, string> paths) => new(
        (page.Value ?? []).Where(item => item.Deleted is null && item.Id != root.Value && paths.ContainsKey(item.Id)).Select(item => DriveItemMapping.ToProviderItem(item, paths[item.Id])).ToList(),
        page.NextLink,
        page.DeltaLink is { } link ? new DeltaToken(link) : null);

    /// <summary>
    /// A deleted facet is <see cref="ChangeKind.Deleted"/> with no item. Otherwise an item whose created and modified times match is <see cref="ChangeKind.Created"/>, anything else <see cref="ChangeKind.Modified"/>.
    /// Graph does not say what an item was called before, so a rename or move is reported as <see cref="ChangeKind.Modified"/> with the new path.
    /// </summary>
    public static ProviderChange ToChange(GraphDriveItem item, IReadOnlyDictionary<string, string> paths) => item.Deleted is not null
        ? new ProviderChange(ChangeKind.Deleted, new RemoteItemId(item.Id))
        : new ProviderChange(item.CreatedDateTime == item.LastModifiedDateTime ? ChangeKind.Created : ChangeKind.Modified, new RemoteItemId(item.Id), DriveItemMapping.ToProviderItem(item, paths[item.Id]));
}
