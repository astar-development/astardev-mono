using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where logs, spans and metric samples are kept for the in-app screens, apart from the sync state.
/// Failure is a <see cref="StoreError"/> value, never an exception; cancellation propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface ITelemetryStore
{
    /// <summary>Stores a batch in one transaction.</summary>
    /// <param name="entries">The entries to keep. An empty batch succeeds without touching the file.</param>
    /// <param name="cancellationToken">Cancelling before the commit stores nothing.</param>
    /// <returns>Success, or <see cref="StorageFailed"/> when the telemetry database is unavailable. Nothing is stored on failure.</returns>
    Task<Result<Unit, StoreError>> AppendAsync(IReadOnlyList<TelemetryEntry> entries, CancellationToken cancellationToken);

    /// <summary>Reads entries matching a filter.</summary>
    /// <param name="query">What to match.</param>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>Matches, newest first and at most <see cref="TelemetryQuery.Limit"/>, or <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<TelemetryEntry>, StoreError>> QueryAsync(TelemetryQuery query, CancellationToken cancellationToken);

    /// <summary>Deletes entries of every kind stamped before a cutoff.</summary>
    /// <param name="olderThan">Entries stamped strictly before this instant are deleted.</param>
    /// <param name="cancellationToken">Cancelling before the commit deletes nothing.</param>
    /// <returns>How many entries were deleted, or <see cref="StorageFailed"/>.</returns>
    Task<Result<int, StoreError>> PruneAsync(DateTimeOffset olderThan, CancellationToken cancellationToken);
}
