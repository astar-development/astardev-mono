namespace AStarDev.CloudSync.Domain;

/// <summary>
/// The provider's stable identity for a file or folder. It survives renames and moves, so compare items by this and never by path.
/// </summary>
/// <param name="Value">Opaque provider text, such as a OneDrive item id. Do not parse it or assume its format.</param>
public readonly record struct RemoteItemId(string Value);
