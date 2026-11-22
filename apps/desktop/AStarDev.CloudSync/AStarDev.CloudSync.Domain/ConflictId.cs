namespace AStarDev.CloudSync.Domain;

/// <summary>Our own identity for one conflict.</summary>
/// <param name="Value">Unique per conflict.</param>
public readonly record struct ConflictId(Guid Value);
