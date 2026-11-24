namespace AStarDev.CloudSync.Classification;

/// <summary>A word found in file paths that is not yet a term. The word can come from a user's file names, so <see cref="ToString"/> is redacted.</summary>
/// <param name="Word">The lower-cased word, for display.</param>
/// <param name="FileCount">The number of distinct files whose path contains the word. At least one.</param>
/// <param name="Hint">What the UI can show next to the word.</param>
public sealed record ClassificationSuggestion(string Word, int FileCount, SuggestionHint Hint)
{
    /// <summary>Returns a redacted form, because the word can come from a user's file names.</summary>
    public override string ToString() => "ClassificationSuggestion(redacted)";
}
