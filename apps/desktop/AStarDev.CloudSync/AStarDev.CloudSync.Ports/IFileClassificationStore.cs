using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where the links between items and terms are kept. An automatic link comes from classification and is replaced on the next run; a user link was chosen by the user and automatic replacement never removes it.
/// Failure is a <see cref="ClassificationError"/> value, never an exception; cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IFileClassificationStore
{
    /// <summary>Replaces the automatic links of many items. The work is written in transactions of at most 1,000 items, so a large call never holds the database for long.</summary>
    /// <param name="updates">The items and the terms that now apply to each.</param>
    /// <param name="cancellationToken">Cancelling stops between transactions; the ones already written stay.</param>
    /// <returns>Success, or <see cref="StorageFailed"/>. An item that is no longer stored is skipped, and a term the user already linked is left as a user link.</returns>
    Task<Result<Unit, ClassificationError>> ReplaceAutoLinksAsync(IReadOnlyList<ItemTermLinks> updates, CancellationToken cancellationToken);

    /// <summary>Links a term to an item as the user's own choice. An automatic link to the same term becomes a user link. Safe to call twice.</summary>
    /// <param name="itemId">The item.</param>
    /// <param name="termId">The term.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the links unchanged.</param>
    /// <returns>Success, <see cref="ItemNotFound"/>, <see cref="TermNotFound"/>, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, ClassificationError>> AssignUserLinkAsync(ItemId itemId, TermId termId, CancellationToken cancellationToken);

    /// <summary>Reads the terms linked to an item.</summary>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The term identities; empty when the item is unclassified or unknown. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<TermId>, ClassificationError>> GetTermsForItemAsync(ItemId itemId, CancellationToken cancellationToken);

    /// <summary>Reads the items linked to a term.</summary>
    /// <param name="termId">The term.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The item identities; empty when no item has the term or the term is unknown. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<ItemId>, ClassificationError>> GetItemsForTermAsync(TermId termId, CancellationToken cancellationToken);

    /// <summary>Counts the items linked to each term.</summary>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The count per term; a term with no items is absent. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyDictionary<TermId, int>, ClassificationError>> GetFileCountsAsync(CancellationToken cancellationToken);
}
