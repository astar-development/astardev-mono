namespace AStarDev.CloudSync.Domain;

/// <summary>Our own identity for a synchronised file or folder. It does not change when the item is renamed or moved, unlike its <see cref="RelativePath"/>.</summary>
/// <param name="Value">Unique per item. Not the provider's id; that is <see cref="RemoteItemId"/>.</param>
public readonly record struct ItemId(Guid Value);
