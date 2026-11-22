namespace AStarDev.CloudSync.Domain;

/// <summary>What the provider shows for an item right now.</summary>
/// <param name="ItemId">The provider's identity for the item.</param>
/// <param name="Path">Where the item is remotely.</param>
/// <param name="Size">Content size in bytes.</param>
/// <param name="ModifiedUtc">Last-modified time of the content.</param>
/// <param name="ETag">The provider's content version.</param>
/// <param name="CTag">The provider's metadata version. Null when the provider gives none.</param>
public sealed record RemoteSnapshot(RemoteItemId ItemId, RelativePath Path, long Size, DateTimeOffset ModifiedUtc, ETag ETag, CTag? CTag);
