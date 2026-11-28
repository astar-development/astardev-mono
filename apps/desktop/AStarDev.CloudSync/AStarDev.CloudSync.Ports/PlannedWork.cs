using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>The operations planned for one item and the baseline the item takes when they are done.</summary>
/// <param name="Operations">The operations to queue, in order; never empty. All belong to the same item, and the target is staged against the last one, as <see cref="IKnownSnapshotStore.StageAsync"/> does.</param>
/// <param name="Target">The baseline once the last operation succeeds; <see langword="null"/> removes the baseline, as after a delete.</param>
public sealed record PlannedWork(IReadOnlyList<Operation> Operations, KnownSnapshot? Target);
