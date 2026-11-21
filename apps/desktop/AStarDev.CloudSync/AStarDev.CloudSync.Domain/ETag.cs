namespace AStarDev.CloudSync.Domain;

/// <summary>The provider's version marker for an item's content. A different value means the content changed.</summary>
/// <param name="Value">Opaque provider text. Compare for equality only.</param>
public readonly record struct ETag(string Value);
