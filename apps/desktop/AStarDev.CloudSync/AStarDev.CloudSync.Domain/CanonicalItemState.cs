namespace AStarDev.CloudSync.Domain;

/// <summary>Everything the reconciler may reason from for one item: the baseline and both current observations. A null snapshot means the item is missing there.</summary>
/// <param name="ItemId">Our identity for the item.</param>
/// <param name="Known">The last reconciled state. Null when the item was never reconciled.</param>
/// <param name="Local">The current local observation. Null when the item is absent locally.</param>
/// <param name="Remote">The current remote observation. Null when the item is absent remotely.</param>
/// <param name="LocalObserved">Whether the disk was ever observed for the item. When false, a null <paramref name="Local"/> means not looked at yet, never that the file was deleted. Defaults to true.</param>
public sealed record CanonicalItemState(ItemId ItemId, KnownSnapshot? Known, LocalSnapshot? Local, RemoteSnapshot? Remote, bool LocalObserved = true);
