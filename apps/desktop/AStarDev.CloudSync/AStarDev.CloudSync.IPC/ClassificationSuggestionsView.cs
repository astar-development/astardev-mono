namespace AStarDev.CloudSync.IPC;

/// <summary>The unreviewed words found in file paths, answering <see cref="GetClassificationSuggestions"/>.</summary>
/// <param name="Suggestions">The most common words, ordered by file count descending, then alphabetically. The daemon returns at most 500.</param>
/// <param name="Total">How many words were found in all; more than <c>Suggestions.Count</c> when the list was cut.</param>
public sealed record ClassificationSuggestionsView(IReadOnlyList<ClassificationSuggestionView> Suggestions, int Total);
