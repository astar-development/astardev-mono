namespace AStarDev.CloudSync.IPC;

/// <summary>The unreviewed words found in file paths, answering <see cref="GetClassificationSuggestions"/>.</summary>
/// <param name="Suggestions">The words ordered by file count descending, then alphabetically.</param>
public sealed record ClassificationSuggestionsView(IReadOnlyList<ClassificationSuggestionView> Suggestions);
