using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetConflicts"/> from the <see cref="IConflictStore"/> and the item state.</summary>
internal sealed class ConflictsQuery(IConflictStore store, ConflictViewFactory views)
{
    /// <summary>Lists the conflicts of one job that still wait for the user, deferred ones included.</summary>
    /// <returns>The conflicts oldest first with their path and sides, empty for an unknown job, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ConflictsView, IpcError>> HandleAsync(GetConflicts query, CancellationToken cancellationToken)
    {
        var jobId = new SyncJobId(query.SyncJobId);

        return await store.ListOpenAsync(jobId, cancellationToken) switch
        {
            Ok<IReadOnlyList<Conflict>> ok => await DescribeAsync(jobId, ok.Value, cancellationToken),
            Fail<StoreError> fail => Failed(fail.Error)
        };
    }

    private async Task<Result<ConflictsView, IpcError>> DescribeAsync(SyncJobId jobId, IReadOnlyList<Conflict> conflicts, CancellationToken cancellationToken)
        => await views.CreateAsync(jobId, conflicts, cancellationToken) switch
        {
            Ok<IReadOnlyList<ConflictView>> ok => new Ok<ConflictsView>(new ConflictsView(ok.Value)),
            Fail<StoreError> fail => Failed(fail.Error)
        };

    private static Fail<IpcError> Failed(StoreError error) => new(new HandlerFailed(StoreErrorMessages.Describe(error, "read", "conflicts")));
}
