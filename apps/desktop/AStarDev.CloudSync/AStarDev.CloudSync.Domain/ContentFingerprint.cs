namespace AStarDev.CloudSync.Domain;

/// <summary>A digest of an item's content. Equal fingerprints mean equal content regardless of timestamps.</summary>
/// <param name="Value">Opaque digest text. Compare for equality only, and only between fingerprints made by the same algorithm.</param>
public readonly record struct ContentFingerprint(string Value);
