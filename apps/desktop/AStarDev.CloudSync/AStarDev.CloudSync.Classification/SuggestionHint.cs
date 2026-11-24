namespace AStarDev.CloudSync.Classification;

/// <summary>What the suggestions list can tell the user about a suggested word.</summary>
public enum SuggestionHint
{
    /// <summary>Nothing special is known about the word.</summary>
    Plain,

    /// <summary>The word is a known first name, so the UI can hint at a person. It is never assigned automatically.</summary>
    FirstName
}
