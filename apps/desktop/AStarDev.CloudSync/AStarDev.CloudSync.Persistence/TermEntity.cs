using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored form of a <see cref="Term"/>. A category holds each text once.</summary>
internal sealed class TermEntity
{
    /// <summary>Primary key; stays the same when the term is edited.</summary>
    public TermId Id { get; set; }

    /// <summary>The normalised term text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The category the term belongs to; a category that still holds terms cannot be deleted.</summary>
    public CategoryId CategoryId { get; set; }

    /// <summary>Where the term came from.</summary>
    public TermSource Source { get; set; }

    /// <summary>Whether the term classifies files or is ignored.</summary>
    public TermStatus Status { get; set; }

    /// <summary>Copies the domain term into its stored form.</summary>
    /// <param name="term">The term to store.</param>
    /// <returns>A new, untracked entity.</returns>
    public static TermEntity From(Term term) => new() { Id = term.Id, Text = term.Text, CategoryId = term.CategoryId, Source = term.Source, Status = term.Status };

    /// <summary>Builds the domain term from the stored row.</summary>
    /// <returns>An immutable term with the same values.</returns>
    public Term ToTerm() => new(Id, Text, CategoryId, Source, Status);

    /// <summary>Overwrites the stored values with those of <paramref name="term"/>; the identity is kept.</summary>
    /// <param name="term">The term holding the new values.</param>
    public void Apply(Term term)
    {
        Text = term.Text;
        CategoryId = term.CategoryId;
        Source = term.Source;
        Status = term.Status;
    }
}
