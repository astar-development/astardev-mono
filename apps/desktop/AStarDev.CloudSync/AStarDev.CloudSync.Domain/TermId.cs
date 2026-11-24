namespace AStarDev.CloudSync.Domain;

/// <summary>Our own identity for one classification term.</summary>
/// <param name="Value">Unique per term. Stays the same when the term is renamed or moved to another category.</param>
public readonly record struct TermId(Guid Value);
