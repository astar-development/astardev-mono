using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetConflicts"/> from the <see cref="IConflictStore"/>.</summary>
internal sealed class ConflictsQuery(IConflictStore store)
{
    /// <summary>Lists the open conflicts of one job.</summary>
    /// <returns>The conflicts oldest first, empty for an unknown job, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ConflictsView, IpcError>> HandleAsync(GetConflicts query, CancellationToken cancellationToken)
        => await store.ListOpenAsync(new SyncJobId(query.SyncJobId), cancellationToken) switch
        {
            Ok<IReadOnlyList<Conflict>> ok => new Ok<ConflictsView>(new ConflictsView([.. ok.Value.Select(ToView)])),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read", "conflicts")))
        };

    internal static ConflictView ToView(Conflict conflict) => new(conflict.Id.Value, conflict.SyncJobId.Value, conflict.ItemId.Value, conflict.Type.ToString(), conflict.CreatedUtc);
}
