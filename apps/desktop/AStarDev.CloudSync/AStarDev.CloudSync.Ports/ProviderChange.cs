using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>One change to a remote item, as the provider reports it. A report of remote state, never a decision about what to do with it.</summary>
/// <param name="Kind">What happened to the item.</param>
/// <param name="RemoteItemId">The provider's identity for the item. Always set, also for <see cref="ChangeKind.Deleted"/>.</param>
/// <param name="Item">The item as it is now. <see langword="null"/> for <see cref="ChangeKind.Deleted"/>, set for every other kind.</param>
/// <param name="PreviousPath">Where a <see cref="ChangeKind.Renamed"/> item used to sit. <see langword="null"/> for other kinds, and for renames when the provider does not report the old path.</param>
public sealed record ProviderChange(ChangeKind Kind, RemoteItemId RemoteItemId, ProviderItem? Item = null, RelativePath? PreviousPath = null);
