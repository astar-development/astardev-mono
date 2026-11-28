namespace AStarDev.CloudSync.IPC;

/// <summary>One page of the unreviewed words found in file paths, answering <see cref="GetClassificationSuggestions"/>.</summary>
/// <param name="Suggestions">The words of this page, ordered by file count descending, then alphabetically, so pages read one after another neither repeat nor skip a word while the paths are unchanged. Empty when the page is past the end.</param>
/// <param name="Total">How many words were found in all, so a caller can ask for the next page. At least <c>Suggestions.Count</c>.</param>
public sealed record ClassificationSuggestionsView(IReadOnlyList<ClassificationSuggestionView> Suggestions, int Total);
