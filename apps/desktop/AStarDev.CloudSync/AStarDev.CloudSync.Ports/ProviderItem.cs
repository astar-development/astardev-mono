using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>One file or folder as the provider reports it. A report of remote state, never a decision about what to do with it.</summary>
/// <param name="RemoteItemId">The provider's stable identity for the item.</param>
/// <param name="Path">Where the item sits now. Changes when the item is renamed or moved.</param>
/// <param name="ItemType">File or folder.</param>
/// <param name="Size">Content size in bytes.</param>
/// <param name="ModifiedUtc">When the provider last recorded a change to the item, in UTC.</param>
public sealed record ProviderItem(RemoteItemId RemoteItemId, RelativePath Path, ItemType ItemType, long Size, DateTimeOffset ModifiedUtc);
