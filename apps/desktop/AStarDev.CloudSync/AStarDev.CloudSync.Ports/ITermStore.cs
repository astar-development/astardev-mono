using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where classification terms are kept between runs. Failure is a <see cref="ClassificationError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface ITermStore
{
    /// <summary>Lists every term, including ignored ones.</summary>
    /// <param name="cancellationToken">Cancelling abandons the listing.</param>
    /// <returns>The terms ordered by text; empty when none exists. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<Term>, ClassificationError>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Stores a new term.</summary>
    /// <param name="term">The term, with its own identity.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves nothing stored.</param>
    /// <returns>Success, <see cref="CategoryNotFound"/> when its category is not stored, <see cref="TermAlreadyExists"/> when the category holds the text or the identity is taken, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, ClassificationError>> AddAsync(Term term, CancellationToken cancellationToken);

    /// <summary>Replaces the text, category, source and status of a stored term. Its links stay.</summary>
    /// <param name="term">The term with its new values, found by <see cref="Term.Id"/>.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the old values.</param>
    /// <returns>Success, <see cref="TermNotFound"/>, <see cref="CategoryNotFound"/>, <see cref="TermAlreadyExists"/> when the category already holds the text, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, ClassificationError>> UpdateAsync(Term term, CancellationToken cancellationToken);

    /// <summary>Removes a term and every link to it.</summary>
    /// <param name="id">The term to remove.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the term stored.</param>
    /// <returns>Success, <see cref="TermNotFound"/>, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, ClassificationError>> RemoveAsync(TermId id, CancellationToken cancellationToken);
}
