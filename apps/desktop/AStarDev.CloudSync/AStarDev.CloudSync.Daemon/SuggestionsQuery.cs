using AStarDev.CloudSync.Classification;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetClassificationSuggestions"/> from <see cref="IClassificationSuggestions"/>.</summary>
internal sealed class SuggestionsQuery(IClassificationSuggestions suggestions)
{
    /// <summary>Scans the file paths for unreviewed words.</summary>
    /// <returns>The words ranked by file count, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ClassificationSuggestionsView, IpcError>> HandleAsync(GetClassificationSuggestions query, CancellationToken cancellationToken)
        => await suggestions.GetSuggestionsAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<ClassificationSuggestion>> found => new Ok<ClassificationSuggestionsView>(new ClassificationSuggestionsView([.. found.Value.Select(suggestion => new ClassificationSuggestionView(suggestion.Word, suggestion.FileCount, suggestion.Hint == SuggestionHint.FirstName))])),
            Fail<ClassificationError> fail => new Fail<IpcError>(new HandlerFailed(ClassificationErrorMessages.Describe(fail.Error, "scanned", "file paths")))
        };
}
