using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Decides items that were never reconciled for a Download or Upload job, where one side is authoritative. Pure: no I/O, no state.</summary>
internal static class BootstrapResolver
{
    /// <summary>
    /// Decides one item that has no baseline. The authoritative side is copied over the other when the item is missing there or differs; an item that exists only on the other side is left alone, never deleted.
    /// </summary>
    /// <param name="mode">Download or Upload; Merge is decided by the reconciler, not here.</param>
    /// <param name="state">The item, with no baseline.</param>
    /// <returns>A resolved decision; never a conflict.</returns>
    public static SyncDecision Resolve(BootstrapMode mode, CanonicalItemState state)
        => (mode, state.Local, state.Remote) switch
        {
            (_, null, null) => Apply(SyncDecisionType.NoOp),
            (BootstrapMode.Download, null, not null) => Apply(SyncDecisionType.CreateLocal),
            (BootstrapMode.Download, not null, not null) => IsSame(state) ? Apply(SyncDecisionType.NoOp) : Apply(SyncDecisionType.UpdateLocal),
            (BootstrapMode.Upload, not null, null) => Apply(SyncDecisionType.CreateRemote),
            (BootstrapMode.Upload, not null, not null) => IsSame(state) ? Apply(SyncDecisionType.NoOp) : Apply(SyncDecisionType.UpdateRemote),
            _ => Apply(SyncDecisionType.NoOp)
        };

    private static bool IsSame(CanonicalItemState state)
        => state.Local is { } local && state.Remote is { } remote && local.Path == remote.Path && local.Fingerprint is { } fingerprint && fingerprint == remote.Fingerprint;

    private static SyncDecision Apply(SyncDecisionType step) => new Resolved([step]);
}
