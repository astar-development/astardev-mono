using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>Everything one planning batch of a job decided, written together by <see cref="IPlanStore.SaveAsync"/>.</summary>
/// <param name="JobId">The job that was planned.</param>
/// <param name="Forgotten">Items gone from both sides whose baseline and leftover item rows are removed, as <see cref="IKnownSnapshotStore.RemoveAsync"/> does.</param>
/// <param name="Work">The operations to queue, grouped by item with the baseline each item takes once its last operation succeeds.</param>
/// <param name="Conflicts">The conflicts to store, each with the journal entry written only when it is stored.</param>
public sealed record PlanBatch(SyncJobId JobId, IReadOnlyList<ItemId> Forgotten, IReadOnlyList<PlannedWork> Work, IReadOnlyList<PlannedConflict> Conflicts)
{
    /// <summary><see langword="true"/> when the batch has nothing to write, so saving it can be skipped.</summary>
    public bool IsEmpty => Forgotten.Count == 0 && Work.Count == 0 && Conflicts.Count == 0;
}
