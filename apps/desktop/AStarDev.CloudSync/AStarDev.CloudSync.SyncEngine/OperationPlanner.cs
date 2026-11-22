using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Turns a resolved decision into the operations that carry it out and the baseline that follows their success. Pure: no I/O, no state.</summary>
internal static class OperationPlanner
{
    private static readonly Dictionary<SyncDecisionType, PlannedOperation> Operations = new()
    {
        [SyncDecisionType.CreateLocal] = new(OperationType.DownloadFile, OperationPriority.Background),
        [SyncDecisionType.UpdateLocal] = new(OperationType.DownloadFile, OperationPriority.Background),
        [SyncDecisionType.CreateRemote] = new(OperationType.UploadFile, OperationPriority.Background),
        [SyncDecisionType.UpdateRemote] = new(OperationType.UploadFile, OperationPriority.Background),
        [SyncDecisionType.RenameLocal] = new(OperationType.RenameLocal, OperationPriority.Interactive),
        [SyncDecisionType.RenameRemote] = new(OperationType.RenameRemote, OperationPriority.Interactive),
        [SyncDecisionType.DeleteLocal] = new(OperationType.DeleteLocal, OperationPriority.Background),
        [SyncDecisionType.DeleteRemote] = new(OperationType.DeleteRemote, OperationPriority.Background)
    };

    /// <summary>Plans one item.</summary>
    /// <param name="state">The state the decision was made from; a merge is expanded from it.</param>
    /// <param name="decision">The resolved decision.</param>
    /// <returns>
    /// The operations in run order, and the baseline for the last one to commit. A merge becomes the rename of the side that moved the item plus the content update of the other side.
    /// <see cref="ItemPlan.Nothing"/> when the decision asks for no action.
    /// </returns>
    public static ItemPlan Plan(CanonicalItemState state, Resolved decision)
    {
        var steps = decision.Steps.SelectMany(step => Expand(state, step)).ToList();
        var operations = steps.Select(ToOperation).OfType<PlannedOperation>().ToList();
        if (operations.Count == 0) return ItemPlan.Nothing;

        var isDelete = steps.Any(step => step is SyncDecisionType.DeleteLocal or SyncDecisionType.DeleteRemote);
        var target = isDelete ? null : BaselineAfter(state, steps);

        return !isDelete && target is null ? ItemPlan.Nothing : new ItemPlan(operations, target);
    }

    private static IEnumerable<SyncDecisionType> Expand(CanonicalItemState state, SyncDecisionType step)
    {
        if (step != SyncDecisionType.Merge) return [step];

        return state.Local is { } local && state.Known is { } known && local.Path != known.Path
            ? [SyncDecisionType.RenameRemote, SyncDecisionType.UpdateLocal]
            : [SyncDecisionType.RenameLocal, SyncDecisionType.UpdateRemote];
    }

    private static PlannedOperation? ToOperation(SyncDecisionType step) => Operations.GetValueOrDefault(step);

    private static KnownSnapshot? BaselineAfter(CanonicalItemState state, IReadOnlyList<SyncDecisionType> steps)
    {
        var fromRemote = steps.Any(step => step is SyncDecisionType.CreateLocal or SyncDecisionType.UpdateLocal);
        var fromLocal = steps.Any(step => step is SyncDecisionType.CreateRemote or SyncDecisionType.UpdateRemote);
        var path = steps.Contains(SyncDecisionType.RenameRemote) ? state.Local?.Path
            : steps.Contains(SyncDecisionType.RenameLocal) ? state.Remote?.Path
            : fromRemote ? state.Remote?.Path
            : state.Local?.Path ?? state.Known?.Path;
        if (path is not { } target) return null;

        if (fromRemote && state.Remote is { } remote) return new KnownSnapshot(target, remote.Size, remote.ModifiedUtc, remote.ETag.Value.Length > 0 ? remote.ETag : null, remote.Fingerprint);
        if (fromLocal && state.Local is { } local) return new KnownSnapshot(target, local.Size, local.ModifiedUtc, null, local.Fingerprint);

        return state.Known is { } known ? known with { Path = target } : null;
    }
}
