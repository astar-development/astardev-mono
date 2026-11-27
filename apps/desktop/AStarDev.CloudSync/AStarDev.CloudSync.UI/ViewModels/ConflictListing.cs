using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Reads the conflicts of every sync job from the daemon. The one place that knows conflicts are asked for per job.</summary>
internal static class ConflictListing
{
    /// <summary>Asks for the jobs, then for the first page of the conflicts of each.</summary>
    /// <param name="client">A connected client.</param>
    /// <param name="cancellationToken">Cancelling abandons the reads and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>The first page of each job's open and deferred conflicts, oldest first, keyed by job id with the job's total, empty when there are no jobs; or the first failure.</returns>
    public static Task<Result<IReadOnlyDictionary<Guid, ConflictsView>, IpcError>> ListAsync(IIpcClient client, CancellationToken cancellationToken) => ListAsync(client, _ => 0, cancellationToken);

    /// <summary>Asks for the jobs, then for the page of each job's conflicts that starts where <paramref name="skip"/> says.</summary>
    /// <param name="client">A connected client.</param>
    /// <param name="skip">How many of a job's conflicts are already shown, so its next page starts after them.</param>
    /// <param name="cancellationToken">Cancelling abandons the reads and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>One page of each job's open and deferred conflicts, oldest first, keyed by job id with the job's total, empty when there are no jobs; or the first failure.</returns>
    public static async Task<Result<IReadOnlyDictionary<Guid, ConflictsView>, IpcError>> ListAsync(IIpcClient client, Func<Guid, int> skip, CancellationToken cancellationToken)
    {
        var jobs = await client.QueryAsync(new GetSyncJobs(), cancellationToken);
        if (jobs.Value is Fail<IpcError> jobsFailure) return jobsFailure;

        var pages = new Dictionary<Guid, ConflictsView>();

        foreach (var job in ((Ok<SyncJobsView>)jobs.Value).Value.Jobs)
        {
            var listed = await client.QueryAsync(new GetConflicts(job.Id, skip(job.Id)), cancellationToken);
            if (listed.Value is Fail<IpcError> failure) return failure;

            pages[job.Id] = ((Ok<ConflictsView>)listed.Value).Value;
        }

        return new Ok<IReadOnlyDictionary<Guid, ConflictsView>>(pages);
    }
}
