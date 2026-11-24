using AStarDev.CloudSync.Classification;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetClassificationSuggestions"/> from <see cref="IClassificationSuggestions"/>.</summary>
internal sealed class SuggestionsQuery(IClassificationSuggestions suggestions)
{
    private const int MaxSuggestions = 500;

    /// <summary>Scans the file paths for unreviewed words.</summary>
    /// <returns>The 500 most common words ranked by file count, with how many were found in all, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ClassificationSuggestionsView, IpcError>> HandleAsync(GetClassificationSuggestions query, CancellationToken cancellationToken)
        => await suggestions.GetSuggestionsAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<ClassificationSuggestion>> found => new Ok<ClassificationSuggestionsView>(new ClassificationSuggestionsView([.. found.Value.Take(MaxSuggestions).Select(suggestion => new ClassificationSuggestionView(suggestion.Word, suggestion.FileCount, suggestion.Hint == SuggestionHint.FirstName))], found.Value.Count)),
            Fail<ClassificationError> fail => new Fail<IpcError>(new HandlerFailed(ClassificationErrorMessages.Describe(fail.Error, "scanned", "file paths")))
        };
}
