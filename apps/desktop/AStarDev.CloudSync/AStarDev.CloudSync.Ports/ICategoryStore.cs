using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where classification categories are kept between runs. Failure is a <see cref="ClassificationError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface ICategoryStore
{
    /// <summary>Lists every category.</summary>
    /// <param name="cancellationToken">Cancelling abandons the listing.</param>
    /// <returns>The categories ordered by name, ignoring case; empty when none exists. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<Category>, ClassificationError>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Stores a new category.</summary>
    /// <param name="category">The category, with its own identity.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves nothing stored.</param>
    /// <returns>Success, <see cref="CategoryAlreadyExists"/> when the identity or the name, ignoring case, is taken, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, ClassificationError>> AddAsync(Category category, CancellationToken cancellationToken);

    /// <summary>Gives a category a new name.</summary>
    /// <param name="id">The category to rename.</param>
    /// <param name="name">The new name.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the old name.</param>
    /// <returns>Success, <see cref="CategoryNotFound"/>, <see cref="CategoryAlreadyExists"/> when another category has the name, ignoring case, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, ClassificationError>> RenameAsync(CategoryId id, string name, CancellationToken cancellationToken);

    /// <summary>Removes a category that holds no terms.</summary>
    /// <param name="id">The category to remove.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the category stored.</param>
    /// <returns>Success, <see cref="CategoryNotFound"/>, <see cref="CategoryNotEmpty"/> when it still holds terms, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, ClassificationError>> RemoveAsync(CategoryId id, CancellationToken cancellationToken);
}
