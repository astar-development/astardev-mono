using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetConflicts"/> from the <see cref="IConflictStore"/> and the item state.</summary>
internal sealed class ConflictsQuery(IConflictStore store, ConflictViewFactory views)
{
    /// <summary>Reads the requested page of the conflicts of one job that still wait for the user, deferred ones included, so one answer fits an IPC frame.</summary>
    /// <returns>The page oldest first with their path and sides and how many the job has in all, empty for an unknown job; a negative <see cref="GetConflicts.Skip"/> counts as zero and a <see cref="GetConflicts.Take"/> outside 1 to <see cref="ApplicationConstants.ListLimit"/> is brought into that range. <see cref="HandlerFailed"/> carries the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ConflictsView, IpcError>> HandleAsync(GetConflicts query, CancellationToken cancellationToken)
    {
        var jobId = new SyncJobId(query.SyncJobId);

        return await store.PageOpenAsync(jobId, Paging.Skip(query.Skip), Paging.Take(query.Take), cancellationToken) switch
        {
            Ok<Page<Conflict>> ok => await DescribeAsync(jobId, ok.Value, cancellationToken),
            Fail<StoreError> fail => Failed(fail.Error)
        };
    }

    private async Task<Result<ConflictsView, IpcError>> DescribeAsync(SyncJobId jobId, Page<Conflict> page, CancellationToken cancellationToken)
        => await views.CreateAsync(jobId, page.Items, cancellationToken) switch
        {
            Ok<IReadOnlyList<ConflictView>> ok => new Ok<ConflictsView>(new ConflictsView(page.Total, ok.Value)),
            Fail<StoreError> fail => Failed(fail.Error)
        };

    private static Fail<IpcError> Failed(StoreError error) => new(new HandlerFailed(StoreErrorMessages.Describe(error, "read", "conflicts")));
}
