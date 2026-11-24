using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification;

/// <summary>Finds words in file paths that are not yet terms, and lets the user promote or ignore them. Nothing is ever assigned automatically.</summary>
public interface IClassificationSuggestions
{
    /// <summary>Scans every stored file path and ranks the unreviewed words.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The words ordered by file count descending, then alphabetically. A word that is already a term, in any status, or a common English word is left out. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<ClassificationSuggestion>, ClassificationError>> GetSuggestionsAsync(CancellationToken cancellationToken);

    /// <summary>Makes the word an active user term in the category, or promotes the existing suggested or ignored term. Safe to call twice and from parallel callers: they end with one term.</summary>
    /// <param name="word">The word to promote; it is normalised like any term text.</param>
    /// <param name="categoryId">The category to put the term in.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The term, <see cref="InvalidTermText"/> when the word is blank, <see cref="CategoryNotFound"/>, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Term, ClassificationError>> PromoteAsync(string word, CategoryId categoryId, CancellationToken cancellationToken);

    /// <summary>Marks the word ignored so it never comes back as a suggestion. The term is created if needed and never deleted. Safe to call twice and from parallel callers.</summary>
    /// <param name="word">The word to ignore; it is normalised like any term text.</param>
    /// <param name="categoryId">The category used when the term has to be created.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The term, <see cref="InvalidTermText"/> when the word is blank, <see cref="CategoryNotFound"/>, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Term, ClassificationError>> IgnoreAsync(string word, CategoryId categoryId, CancellationToken cancellationToken);
}
