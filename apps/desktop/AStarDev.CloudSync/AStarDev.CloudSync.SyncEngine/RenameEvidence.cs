using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>What one observation of an item shows that rename detection may compare.</summary>
/// <param name="Path">Where the item is, or was.</param>
/// <param name="Size">Content size in bytes.</param>
/// <param name="ModifiedUtc">Last-modified time of the content. A rename does not change it.</param>
/// <param name="Fingerprint">Content digest. Null when not computed, which rules out fingerprint matching.</param>
/// <param name="Identity">The stable identity of the side: filesystem identity locally, the provider item id remotely. Null when unavailable.</param>
internal sealed record RenameEvidence(RelativePath Path, long Size, DateTimeOffset ModifiedUtc, ContentFingerprint? Fingerprint, string? Identity);
