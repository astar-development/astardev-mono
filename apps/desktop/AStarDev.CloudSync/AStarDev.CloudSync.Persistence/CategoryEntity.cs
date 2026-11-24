using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored form of a <see cref="Category"/>. The name is unique, ignoring case.</summary>
internal sealed class CategoryEntity
{
    /// <summary>Primary key; stays the same when the category is renamed.</summary>
    public CategoryId Id { get; set; }

    /// <summary>The name shown to the user.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Copies the domain category into its stored form.</summary>
    /// <param name="category">The category to store.</param>
    /// <returns>A new, untracked entity.</returns>
    public static CategoryEntity From(Category category) => new() { Id = category.Id, Name = category.Name };

    /// <summary>Builds the domain category from the stored row.</summary>
    /// <returns>An immutable category with the same values.</returns>
    public Category ToCategory() => new(Id, Name);
}
