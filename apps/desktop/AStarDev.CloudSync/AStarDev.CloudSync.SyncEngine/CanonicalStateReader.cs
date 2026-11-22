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
    {
        var rows = await items.ListAsync(jobId, cancellationToken);
        if (rows.Value is Fail<StoreError> rowsFailure) return rowsFailure;

        var baselines = await known.ListAsync(jobId, cancellationToken);
        if (baselines.Value is Fail<StoreError> baselinesFailure) return baselinesFailure;

        return new Ok<IReadOnlyList<CanonicalItemState>>(CanonicalStateBuilder.Build(((Ok<IReadOnlyList<StoredItem>>)rows.Value).Value, ((Ok<IReadOnlyList<KnownEntry>>)baselines.Value).Value));
    }
}
