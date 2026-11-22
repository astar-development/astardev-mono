using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>What the planner wants done for one item.</summary>
/// <param name="Operations">The operations in the order they must run. Empty when there is nothing to do.</param>
/// <param name="Known">The baseline the item takes once the last operation succeeded; null removes the baseline, as after a delete.</param>
internal sealed record ItemPlan(IReadOnlyList<PlannedOperation> Operations, KnownSnapshot? Known)
{
    internal static ItemPlan Nothing { get; } = new([], null);
}
