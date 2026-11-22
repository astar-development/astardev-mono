using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>One stored item row with its latest local and remote observation, before rows are merged into canonical states.</summary>
/// <param name="ItemId">Our identity for the row.</param>
/// <param name="RemoteItemId">The provider's id; empty text for an item only seen locally.</param>
/// <param name="Path">The row's current path: where the provider has it, or where the disk had it for a local-only item.</param>
/// <param name="Local">The latest local observation; null when the disk was never observed for the row or the file is gone.</param>
/// <param name="LastLocalIdentity">The filesystem identity of the latest local observation, kept when that observation says the file is gone; null when never observed with an identity. It lets rename detection recognise the file at its new path.</param>
/// <param name="Remote">The latest remote observation; null for a local-only item. Its <see cref="RemoteSnapshot.ETag"/> is empty text when none was stored.</param>
public sealed record StoredItem(ItemId ItemId, RemoteItemId RemoteItemId, RelativePath Path, LocalSnapshot? Local, RemoteSnapshot? Remote, LocalIdentity? LastLocalIdentity = null);
