namespace AStarDev.CloudSync.Domain;

/// <summary>What the local filesystem shows for an item right now.</summary>
/// <param name="Path">Where the item is locally.</param>
/// <param name="Size">Content size in bytes.</param>
/// <param name="ModifiedUtc">Last-modified time of the content.</param>
/// <param name="Identity">The filesystem identity. Null when it could not be read.</param>
/// <param name="Fingerprint">Content digest. Null when not computed; it then falls back to size and modified time to detect change.</param>
public sealed record LocalSnapshot(RelativePath Path, long Size, DateTimeOffset ModifiedUtc, LocalIdentity? Identity, ContentFingerprint? Fingerprint);
