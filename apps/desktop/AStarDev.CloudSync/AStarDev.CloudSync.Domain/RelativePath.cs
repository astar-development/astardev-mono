namespace AStarDev.CloudSync.Domain;

/// <summary>Where an item sits relative to the synchronised root. A location that can change, unlike <see cref="RemoteItemId"/>.</summary>
/// <param name="Value">The path text. Never an absolute path on the local disk.</param>
public readonly record struct RelativePath(string Value);
