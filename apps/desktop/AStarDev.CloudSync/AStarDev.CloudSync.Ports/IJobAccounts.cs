using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// Tells which provider account a sync job belongs to, so every call for the job's drive names its own account. Failure is a <see cref="StoreError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IJobAccounts
{
    /// <summary>Finds the provider's id of the account that owns a job.</summary>
    /// <param name="jobId">The job to look up.</param>
    /// <param name="cancellationToken">Cancelling abandons the lookup.</param>
    /// <returns>The <see cref="ProviderAccount.AccountIdentifier"/> to pass to <see cref="ICloudProvider"/>, <see cref="SyncJobNotFound"/> when there is no such job, or <see cref="StorageFailed"/>. Safe to call repeatedly.</returns>
    Task<Result<string, StoreError>> AccountIdentifierAsync(SyncJobId jobId, CancellationToken cancellationToken);
}
