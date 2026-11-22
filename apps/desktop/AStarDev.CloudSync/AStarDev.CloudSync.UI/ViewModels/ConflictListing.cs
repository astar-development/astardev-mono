using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Reads the conflicts of every sync job from the daemon. The one place that knows conflicts are asked for per job.</summary>
internal static class ConflictListing
{
    /// <summary>Asks for the jobs, then for the conflicts of each.</summary>
    /// <param name="client">A connected client.</param>
    /// <param name="cancellationToken">Cancelling abandons the reads and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>Every open and deferred conflict of every job, oldest first, empty when there are no jobs; or the first failure.</returns>
    public static async Task<Result<IReadOnlyList<ConflictView>, IpcError>> ListAsync(IIpcClient client, CancellationToken cancellationToken)
    {
        var jobs = await client.QueryAsync(new GetSyncJobs(), cancellationToken);
        if (jobs.Value is Fail<IpcError> jobsFailure) return jobsFailure;

        var conflicts = new List<ConflictView>();

        foreach (var job in ((Ok<SyncJobsView>)jobs.Value).Value.Jobs)
        {
            var listed = await client.QueryAsync(new GetConflicts(job.Id), cancellationToken);
            if (listed.Value is Fail<IpcError> failure) return failure;

            conflicts.AddRange(((Ok<ConflictsView>)listed.Value).Value.Conflicts);
        }

        return new Ok<IReadOnlyList<ConflictView>>([.. conflicts.OrderBy(conflict => conflict.DetectedUtc)]);
    }
}
