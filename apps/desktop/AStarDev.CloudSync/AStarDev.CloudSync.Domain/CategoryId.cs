namespace AStarDev.CloudSync.Domain;

/// <summary>Our own identity for one classification category.</summary>
/// <param name="Value">Unique per category. Stays the same when the category is renamed.</param>
public readonly record struct CategoryId(Guid Value);
