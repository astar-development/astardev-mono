namespace AStarDev.CloudSync.Domain;

/// <summary>The filesystem's own identity for a local item, such as device and inode. It survives renames on the same volume, unlike the path.</summary>
/// <param name="Value">Opaque text. Compare for equality only; it is not stable across volumes or reformatting.</param>
public readonly record struct LocalIdentity(string Value);
