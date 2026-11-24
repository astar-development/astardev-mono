using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification;

/// <summary>Edits an existing term the way the user asks, applying the same text rules as every other term.</summary>
public interface IClassificationTermEditor
{
    /// <summary>Replaces the text, category and ignored state of a term. Its links stay. A term whose text or category changed becomes a user term; one whose text and category are as before keeps its source.</summary>
    /// <param name="id">The term to edit.</param>
    /// <param name="text">The new text; it is normalised like any term text.</param>
    /// <param name="categoryId">The category the term now belongs to.</param>
    /// <param name="ignored"><see langword="true"/> to ignore the term so it classifies nothing, <see langword="false"/> to make it active.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The edited term, <see cref="InvalidTermText"/> when the text is blank, <see cref="TermNotFound"/>, <see cref="CategoryNotFound"/>, <see cref="TermAlreadyExists"/> when the category already holds the text, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Term, ClassificationError>> UpdateAsync(TermId id, string text, CategoryId categoryId, bool ignored, CancellationToken cancellationToken);
}
