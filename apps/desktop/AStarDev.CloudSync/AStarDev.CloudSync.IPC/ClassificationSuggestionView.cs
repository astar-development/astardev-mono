namespace AStarDev.CloudSync.IPC;

/// <summary>One suggested word. The word comes from the user's file names, so <see cref="ToString"/> is redacted.</summary>
/// <param name="Word">The lower-cased word.</param>
/// <param name="FileCount">How many distinct files have the word in their path.</param>
/// <param name="IsFirstName"><see langword="true"/> when the word is a known first name, so the UI can hint at a person. It is never assigned automatically.</param>
public sealed record ClassificationSuggestionView(string Word, int FileCount, bool IsFirstName)
{
    /// <summary>Returns a redacted form, because the word comes from the user's file names.</summary>
    public override string ToString() => "ClassificationSuggestionView(redacted)";
}
