namespace AStarDev.CloudSync.Domain;

/// <summary>A word or phrase that classifies files into a category. Its text can come from the user's file names, so <see cref="ToString"/> is redacted.</summary>
/// <param name="Id">Our identity for the term.</param>
/// <param name="Text">The normalised text: trimmed, composed, lower case and single spaced.</param>
/// <param name="CategoryId">The category the term belongs to.</param>
/// <param name="Source">Where the term came from.</param>
/// <param name="Status">Whether the term classifies files or is ignored.</param>
public sealed record Term(TermId Id, string Text, CategoryId CategoryId, TermSource Source, TermStatus Status)
{
    /// <summary>Returns a redacted form, because the text can come from a user's file names.</summary>
    public override string ToString() => "Term(redacted)";
}
