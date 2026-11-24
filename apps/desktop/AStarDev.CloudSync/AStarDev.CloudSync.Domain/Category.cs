namespace AStarDev.CloudSync.Domain;

/// <summary>A named group of classification terms, such as colour or place.</summary>
/// <param name="Id">Our identity for the category, stable when it is renamed.</param>
/// <param name="Name">The name shown to the user. Trimmed; keeps its case.</param>
public sealed record Category(CategoryId Id, string Name);
