namespace AStarDev.CloudSync.Domain;

public sealed record ProviderItem(
    RemoteItemId RemoteItemId,
    RelativePath Path,
    ItemType ItemType,
    long Size,
    DateTimeOffset ModifiedUtc);
