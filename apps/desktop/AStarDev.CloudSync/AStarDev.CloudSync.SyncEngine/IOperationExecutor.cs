using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Performs one claimed operation. It only does the work; the scheduler decides about retries and dead letters from the result.</summary>
public interface IOperationExecutor
{
    /// <summary>Performs the operation, which may run again after a crash, so it must be safe to repeat.</summary>
    /// <param name="operation">The claimed operation, already marked running.</param>
    /// <param name="cancellationToken">Cancelling abandons the work and surfaces as <see cref="OperationCanceledException"/>; the operation is then not failed.</param>
    /// <returns>
    /// Success when the work is done. A <see cref="Transient"/> or <see cref="RateLimited"/> failure is retried after a delay until the attempt limit;
    /// any other failure dead-letters the operation at once.
    /// </returns>
    Task<Result<Unit, ProviderError>> ExecuteAsync(Operation operation, CancellationToken cancellationToken);
}
