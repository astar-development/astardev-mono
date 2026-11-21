using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetSyncJobs"/> from the <see cref="ISyncJobStore"/>.</summary>
internal sealed class SyncJobsQuery(ISyncJobStore store)
{
    /// <summary>Lists every stored sync job.</summary>
    /// <returns>The jobs, oldest first, which is none until one is created, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<SyncJobsView, IpcError>> HandleAsync(GetSyncJobs query, CancellationToken cancellationToken)
        => await store.ListAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<SyncJob>> jobs => new Ok<SyncJobsView>(new SyncJobsView([.. jobs.Value.Select(ToView)])),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read", "sync jobs")))
        };

    private static SyncJobView ToView(SyncJob job) => new(job.Id.Value, job.AccountId.Value, job.Name, job.LocalRootPath, BootstrapModes.ToView(job.BootstrapMode));
}
