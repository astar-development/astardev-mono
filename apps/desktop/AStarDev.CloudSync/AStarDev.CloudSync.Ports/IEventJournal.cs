using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// The append-only record of significant occurrences, such as conflicts detected and resolved. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IEventJournal
{
    /// <summary>Adds an entry. Entries are never changed or removed.</summary>
    /// <param name="entry">The entry to keep.</param>
    /// <param name="cancellationToken">Cancelling before the write completes stores nothing.</param>
    /// <returns>Success once stored, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> AppendAsync(JournalEntry entry, CancellationToken cancellationToken);

    /// <summary>Lists what happened to one aggregate.</summary>
    /// <param name="aggregateId">The aggregate whose entries to read.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The entries oldest first; empty for an unknown aggregate; or <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<JournalEntry>, StoreError>> ListAsync(Guid aggregateId, CancellationToken cancellationToken);
}
