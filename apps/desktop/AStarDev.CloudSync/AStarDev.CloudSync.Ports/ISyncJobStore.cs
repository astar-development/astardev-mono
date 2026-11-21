using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Where sync jobs are kept between runs. The MVP holds at most one job. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface ISyncJobStore
{
    /// <summary>Stores a new job, provided no job is stored yet.</summary>
    /// <param name="job">The job to keep.</param>
    /// <param name="cancellationToken">Cancelling before the write completes leaves nothing stored.</param>
    /// <returns>Success, <see cref="SyncJobAlreadyExists"/> when a job is already stored, <see cref="AccountNotFound"/> when the job's account is not stored, or <see cref="StorageFailed"/>.</returns>
    Task<Result<Unit, StoreError>> AddAsync(SyncJob job, CancellationToken cancellationToken);

    /// <summary>Finds one job by our identity.</summary>
    /// <param name="id">The identity to look up.</param>
    /// <param name="cancellationToken">Cancelling abandons the lookup.</param>
    /// <returns>The job, <see cref="SyncJobNotFound"/> when there is none, or <see cref="StorageFailed"/>.</returns>
    Task<Result<SyncJob, StoreError>> GetAsync(SyncJobId id, CancellationToken cancellationToken);

    /// <summary>Lists every stored job.</summary>
    /// <param name="cancellationToken">Cancelling abandons the listing.</param>
    /// <returns>The jobs, oldest first; empty when none was created, which is the state right after an account is connected. Fails only with <see cref="StorageFailed"/>.</returns>
    Task<Result<IReadOnlyList<SyncJob>, StoreError>> ListAsync(CancellationToken cancellationToken);
}
