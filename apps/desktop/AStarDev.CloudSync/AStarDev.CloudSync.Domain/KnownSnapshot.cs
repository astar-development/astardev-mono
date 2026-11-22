namespace AStarDev.CloudSync.Domain;

/// <summary>The last state of an item that both sides were successfully reconciled to. The baseline every change is measured against.</summary>
/// <param name="Path">Where the item sat on both sides at that point.</param>
/// <param name="Size">Content size in bytes.</param>
/// <param name="ModifiedUtc">Last-modified time of the content.</param>
/// <param name="ETag">The provider's content version. Null when none was recorded, in which case size and modified time stand in for it.</param>
/// <param name="Fingerprint">Content digest. Null when none was computed, in which case size and modified time stand in for it.</param>
public sealed record KnownSnapshot(RelativePath Path, long Size, DateTimeOffset ModifiedUtc, ETag? ETag, ContentFingerprint? Fingerprint);
