using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <inheritdoc/>
internal sealed class Reconciler : IReconciler
{
    /// <inheritdoc/>
    /// <remarks>
    /// With no known baseline only creation can be decided, and creations on both sides are the same only when path, size and modified time all match.
    /// With a baseline, each side is compared to it, then the pair is resolved per concern: existence, location and content.
    /// </remarks>
    public SyncDecision Reconcile(CanonicalItemState state)
        => state.Known is null ? ResolveWithoutBaseline(state.Local, state.Remote) : ResolveAgainstBaseline(state.Known, state.Local, state.Remote);

    private static SyncDecision ResolveWithoutBaseline(LocalSnapshot? local, RemoteSnapshot? remote)
        => (local, remote) switch
        {
            (null, null) => Apply(SyncDecisionType.NoOp),
            (not null, null) => Apply(SyncDecisionType.CreateRemote),
            (null, not null) => Apply(SyncDecisionType.CreateLocal),
            (not null, not null) => IsSameCreation(local, remote) ? Apply(SyncDecisionType.NoOp) : new Conflicted(ConflictReason.CreateCreate)
        };

    private static bool IsSameCreation(LocalSnapshot local, RemoteSnapshot remote)
        => local.Path == remote.Path && local.Size == remote.Size && local.ModifiedUtc == remote.ModifiedUtc;

    private static SyncDecision ResolveAgainstBaseline(KnownSnapshot known, LocalSnapshot? local, RemoteSnapshot? remote)
    {
        var localChanges = DetectLocal(known, local);
        var remoteChanges = DetectRemote(known, remote);

        if (localChanges.HasFlag(ChangeSet.Deleted) || remoteChanges.HasFlag(ChangeSet.Deleted)) return ResolveDeletion(localChanges, remoteChanges);
        if (local is null || remote is null) return Apply(SyncDecisionType.NoOp);

        var location = ResolveLocation(localChanges, remoteChanges, local, remote);
        var content = ResolveContent(localChanges, remoteChanges);

        if (location.Conflict is { } locationConflict) return locationConflict;
        if (content.Conflict is { } contentConflict) return contentConflict;

        return Combine([.. location.Steps, .. content.Steps]);
    }

    private static ChangeSet DetectLocal(KnownSnapshot known, LocalSnapshot? local)
    {
        if (local is null) return ChangeSet.Deleted;
        var changes = local.Path == known.Path ? ChangeSet.None : ChangeSet.Renamed;
        var contentChanged = known.Fingerprint is { } knownFingerprint && local.Fingerprint is { } localFingerprint
            ? knownFingerprint != localFingerprint
            : local.Size != known.Size || local.ModifiedUtc != known.ModifiedUtc;

        return contentChanged ? changes | ChangeSet.Modified : changes;
    }

    private static ChangeSet DetectRemote(KnownSnapshot known, RemoteSnapshot? remote)
    {
        if (remote is null) return ChangeSet.Deleted;
        var changes = remote.Path == known.Path ? ChangeSet.None : ChangeSet.Renamed;
        var contentChanged = known.ETag is { } knownETag
            ? knownETag != remote.ETag
            : remote.Size != known.Size || remote.ModifiedUtc != known.ModifiedUtc;

        return contentChanged ? changes | ChangeSet.Modified : changes;
    }

    private static SyncDecision ResolveDeletion(ChangeSet local, ChangeSet remote)
        => (local, remote) switch
        {
            (ChangeSet.Deleted, ChangeSet.Deleted) => Apply(SyncDecisionType.NoOp),
            (ChangeSet.Deleted, ChangeSet.None) => Apply(SyncDecisionType.DeleteRemote),
            (ChangeSet.None, ChangeSet.Deleted) => Apply(SyncDecisionType.DeleteLocal),
            _ => new Conflicted(ConflictReason.KeepDelete)
        };

    private static Outcome ResolveLocation(ChangeSet local, ChangeSet remote, LocalSnapshot localSnapshot, RemoteSnapshot remoteSnapshot)
    {
        var localRenamed = local.HasFlag(ChangeSet.Renamed);
        var remoteRenamed = remote.HasFlag(ChangeSet.Renamed);

        if (localRenamed && remoteRenamed) return localSnapshot.Path == remoteSnapshot.Path ? Outcome.Nothing : Outcome.Clash(ConflictReason.RenameRename);
        if (localRenamed) return Outcome.Of(SyncDecisionType.RenameRemote);

        return remoteRenamed ? Outcome.Of(SyncDecisionType.RenameLocal) : Outcome.Nothing;
    }

    private static Outcome ResolveContent(ChangeSet local, ChangeSet remote)
    {
        var localModified = local.HasFlag(ChangeSet.Modified);
        var remoteModified = remote.HasFlag(ChangeSet.Modified);

        if (localModified && remoteModified) return Outcome.Clash(ConflictReason.ModifyModify);
        if (localModified) return Outcome.Of(SyncDecisionType.UpdateRemote);

        return remoteModified ? Outcome.Of(SyncDecisionType.UpdateLocal) : Outcome.Nothing;
    }

    private static SyncDecision Combine(IReadOnlyList<SyncDecisionType> steps)
        => steps.Count == 0 ? Apply(SyncDecisionType.NoOp) : new Resolved(steps);

    private static SyncDecision Apply(SyncDecisionType step) => new Resolved([step]);

    private sealed record Outcome(IReadOnlyList<SyncDecisionType> Steps, Conflicted? Conflict)
    {
        internal static Outcome Nothing { get; } = new([], null);

        internal static Outcome Of(SyncDecisionType step) => new([step], null);

        internal static Outcome Clash(ConflictReason reason) => new([], new Conflicted(reason));
    }
}
