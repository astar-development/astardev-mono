using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Builds the <see cref="ConflictView"/> the UI shows: the stored conflict plus the item's path and what each side holds, read from the merged item state.</summary>
internal sealed class ConflictViewFactory(CanonicalStateReader states)
{
    /// <summary>Builds the views of conflicts that all belong to one job, in the order given.</summary>
    /// <param name="jobId">The job the conflicts belong to.</param>
    /// <param name="conflicts">The conflicts to describe. Empty reads nothing.</param>
    /// <param name="cancellationToken">Cancelling abandons the read and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>One view per conflict. A conflict whose item the daemon no longer knows gets an empty path and no sides. Or the store's failure.</returns>
    public async Task<Result<IReadOnlyList<ConflictView>, StoreError>> CreateAsync(SyncJobId jobId, IReadOnlyList<Conflict> conflicts, CancellationToken cancellationToken) =>
        conflicts.Count == 0
            ? new Ok<IReadOnlyList<ConflictView>>([])
            : await states.ReadAsync(jobId, cancellationToken) switch
            {
                Ok<IReadOnlyList<CanonicalItemState>> ok => new Ok<IReadOnlyList<ConflictView>>(Describe(conflicts, ok.Value)),
                Fail<StoreError> fail => fail
            };

    /// <summary>Builds the view of a conflict without reading the item state: empty path and no sides.</summary>
    /// <param name="conflict">The conflict to describe.</param>
    /// <returns>A view that carries only what the conflict itself stores.</returns>
    public static ConflictView Bare(Conflict conflict) => ToView(conflict, null);

    private static List<ConflictView> Describe(IReadOnlyList<Conflict> conflicts, IReadOnlyList<CanonicalItemState> items)
    {
        var byItem = items.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.First());

        return [.. conflicts.Select(conflict => ToView(conflict, byItem.GetValueOrDefault(conflict.ItemId)))];
    }

    private static ConflictView ToView(Conflict conflict, CanonicalItemState? state) => new(
        conflict.Id.Value,
        conflict.SyncJobId.Value,
        conflict.ItemId.Value,
        conflict.Type.ToString(),
        conflict.CreatedUtc,
        conflict.Status.ToString(),
        ItemPath(state),
        LocalSide(state),
        RemoteSide(state));

    private static ConflictSideView? RemoteSide(CanonicalItemState? state) =>
        state?.Remote is { } remote ? new ConflictSideView(remote.Path.Value, remote.ModifiedUtc, remote.Size) : null;

    private static ConflictSideView? LocalSide(CanonicalItemState? state) =>
        state?.Local is { } local ? new ConflictSideView(local.Path.Value, local.ModifiedUtc, local.Size) : null;

    private static string ItemPath(CanonicalItemState? state) =>
        state?.Remote?.Path.Value ?? state?.Local?.Path.Value ?? state?.Known?.Path.Value ?? string.Empty;
}
