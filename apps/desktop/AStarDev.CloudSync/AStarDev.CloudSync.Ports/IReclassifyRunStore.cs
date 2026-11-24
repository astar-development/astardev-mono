using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Remembers the last reclassify run that finished. Failure is a <see cref="ClassificationError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IReclassifyRunStore
{
    /// <summary>Reads the last run that finished.</summary>
    /// <param name="cancellationToken">Cancelling abandons the read.</param>
    /// <returns>The run, or none when no run ever finished. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<Option<ReclassifyRun>, ClassificationError>> GetLastCompletedAsync(CancellationToken cancellationToken);

    /// <summary>Records a finished run, replacing the earlier one. A run that was cancelled or failed is never recorded. Safe to call twice.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancelling before the write completes keeps the earlier run.</param>
    /// <returns>Success, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, ClassificationError>> RecordCompletedAsync(ReclassifyRun run, CancellationToken cancellationToken);
}
