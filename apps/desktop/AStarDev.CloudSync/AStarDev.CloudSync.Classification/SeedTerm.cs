namespace AStarDev.CloudSync.Classification;

/// <summary>A term in the shipped seed data. Its text can match a user's file names, so <see cref="ToString"/> is redacted.</summary>
/// <param name="Text">The term text as written in the seed; normalised when stored.</param>
/// <param name="CategoryName">The name of the term's category.</param>
internal sealed record SeedTerm(string Text, string CategoryName)
{
    /// <summary>Returns a redacted form, because the text can match a user's file names.</summary>
    public override string ToString() => "SeedTerm(redacted)";
}
