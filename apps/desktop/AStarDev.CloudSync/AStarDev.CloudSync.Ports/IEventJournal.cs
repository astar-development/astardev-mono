using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// The append-only record, pruned by age, of significant occurrences, such as conflicts detected and resolved. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IEventJournal
{
    /// <summary>Adds an entry. Entries are never changed, and are removed only by <see cref="PruneAsync"/>.</summary>
    /// <param name="entry">The entry to keep.</param>
    /// <param name="cancellationToken">Cancelling before the write completes stores nothing.</param>
    /// <returns>Success once stored, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> AppendAsync(JournalEntry entry, CancellationToken cancellationToken);

    /// <summary>Lists what happened to one aggregate.</summary>
    /// <param name="aggregateId">The aggregate whose entries to read.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The entries oldest first; empty for an unknown aggregate; or <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<JournalEntry>, StoreError>> ListAsync(Guid aggregateId, CancellationToken cancellationToken);

    /// <summary>Lists every entry that occurred in a time range, whatever it is about.</summary>
    /// <param name="fromUtc">The start of the range, inclusive.</param>
    /// <param name="toUtc">The end of the range, inclusive.</param>
    /// <param name="eventTypes">The kinds of occurrence to keep; empty keeps every kind.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The entries oldest first; empty when none match; or <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<JournalEntry>, StoreError>> ListAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, IReadOnlyList<string> eventTypes, CancellationToken cancellationToken);

    /// <summary>Removes the entries that occurred before a moment, which is how the audit retention is enforced. Safe to call twice: the second call removes nothing.</summary>
    /// <param name="olderThanUtc">Entries that occurred strictly before this moment are removed.</param>
    /// <param name="cancellationToken">Cancelling before the delete completes removes nothing.</param>
    /// <returns>How many entries were removed, or <see cref="StorageFailed"/>.</returns>
    Task<Result<int, StoreError>> PruneAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken);
}
