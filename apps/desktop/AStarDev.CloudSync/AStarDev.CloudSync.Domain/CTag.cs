namespace AStarDev.CloudSync.Domain;

/// <summary>The provider's version marker for a folder's children and an item's metadata. It can change when <see cref="ETag"/> does not.</summary>
/// <param name="Value">Opaque provider text. Compare for equality only.</param>
public readonly record struct CTag(string Value);
