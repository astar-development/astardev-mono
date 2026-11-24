namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>One suggested word as text, ready to show as it is. The word comes from the user's file names, so <see cref="ToString"/> is redacted.</summary>
/// <param name="Word">The suggested word.</param>
/// <param name="Detail">How many files have the word in their path, and a hint when it is a first name.</param>
internal sealed record SuggestionRowViewModel(string Word, string Detail)
{
    /// <summary>Returns a redacted form, because the word comes from the user's file names.</summary>
    public override string ToString() => "SuggestionRowViewModel(redacted)";
}
