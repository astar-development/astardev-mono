using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Reads the merged state of every item of a job, the same view the reconciler and the conflict resolver work from, so a caller can show what each side holds for an item.</summary>
public sealed class CanonicalStateReader
{
    private readonly IItemStateStore items;
    private readonly IKnownSnapshotStore known;

    internal CanonicalStateReader(IItemStateStore items, IKnownSnapshotStore known)
    {
        this.items = items;
        this.known = known;
    }

    /// <summary>Reads one state per logical item of the job.</summary>
    /// <param name="jobId">The job whose items to read.</param>
    /// <param name="cancellationToken">Cancelling abandons the read and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>The states, empty for an unknown job or one without items, or the <see cref="StoreError"/> of the store that could not be read.</returns>
    public async Task<Result<IReadOnlyList<CanonicalItemState>, StoreError>> ReadAsync(SyncJobId jobId, CancellationToken cancellationToken)
        => Build(await items.ListAsync(jobId, cancellationToken), await known.ListAsync(jobId, cancellationToken));

    /// <summary>Reads the states of only the given items, so describing a few operations does not read the whole job.</summary>
    /// <remarks>
    /// Only the rows and baselines of the given items are merged, so an item is not joined to a row outside the set. Its path is the one the whole-job read shows;
    /// its sides can differ for an item the whole-job read merges from several rows. Use it to describe items, never to reconcile them.
    /// </remarks>
    /// <param name="jobId">The job the items belong to.</param>
    /// <param name="itemIds">The items to read. An id the job does not know is skipped.</param>
    /// <param name="cancellationToken">Cancelling abandons the read and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>The states of the given items that are known, empty when none, or the <see cref="StoreError"/> of the store that could not be read.</returns>
    public async Task<Result<IReadOnlyList<CanonicalItemState>, StoreError>> ReadAsync(SyncJobId jobId, IReadOnlyCollection<ItemId> itemIds, CancellationToken cancellationToken)
        => Build(await items.ListAsync(jobId, itemIds, cancellationToken), await known.ListAsync(jobId, itemIds, cancellationToken));

    private static Result<IReadOnlyList<CanonicalItemState>, StoreError> Build(Result<IReadOnlyList<StoredItem>, StoreError> rows, Result<IReadOnlyList<KnownEntry>, StoreError> baselines)
    {
        if (rows.Value is Fail<StoreError> rowsFailure) return rowsFailure;
        if (baselines.Value is Fail<StoreError> baselinesFailure) return baselinesFailure;

        return new Ok<IReadOnlyList<CanonicalItemState>>(CanonicalStateBuilder.Build(((Ok<IReadOnlyList<StoredItem>>)rows.Value).Value, ((Ok<IReadOnlyList<KnownEntry>>)baselines.Value).Value));
    }
}
