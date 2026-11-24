using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where the remote folders selected for synchronisation are kept between runs. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface ISyncScopeStore
{
    /// <summary>Selects a remote folder for a job.</summary>
    /// <param name="scope">The folder, its path from the drive root and the job it joins. The path is stored so ownership of local paths survives a restart.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves nothing stored.</param>
    /// <returns>Success, <see cref="SyncJobNotFound"/> when the job is not stored, <see cref="SyncScopeAlreadyExists"/> when the job already holds the folder, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> AddAsync(SyncScope scope, CancellationToken cancellationToken);

    /// <summary>Stops synchronising a remote folder.</summary>
    /// <param name="scope">The folder and the job it leaves.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves the scope stored.</param>
    /// <returns>Success, <see cref="SyncScopeNotFound"/> when the job does not hold the folder, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> RemoveAsync(SyncScope scope, CancellationToken cancellationToken);

    /// <summary>Lists the folders a job synchronises.</summary>
    /// <param name="syncJobId">The job to list.</param>
    /// <param name="cancellationToken">Cancelling abandons the listing.</param>
    /// <returns>The scopes ordered by folder id; empty when none is selected or the job is unknown. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<SyncScope>, StoreError>> ListAsync(SyncJobId syncJobId, CancellationToken cancellationToken);
}
