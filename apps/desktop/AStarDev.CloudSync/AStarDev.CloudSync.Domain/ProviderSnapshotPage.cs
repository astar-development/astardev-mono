namespace AStarDev.CloudSync.Domain;

/// <summary>One page of remote items. S16 adds the resume token.</summary>
public sealed record ProviderSnapshotPage(IReadOnlyList<ProviderItem> Items);
