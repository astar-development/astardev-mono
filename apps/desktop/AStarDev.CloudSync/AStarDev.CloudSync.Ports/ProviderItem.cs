using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

public sealed record ProviderItem(
    RemoteItemId RemoteItemId,
    RelativePath Path,
    ItemType ItemType,
    long Size,
    DateTimeOffset ModifiedUtc);
