using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Turns the user's choice for a conflicted item into the steps the normal operation planner expands. Pure: no I/O, no state.</summary>
internal static class ResolutionPlanner
{
    /// <summary>Picks the steps that carry out a choice. Keep both applies the remote side, because the local version is copied away first, see <see cref="NeedsCopy"/>.</summary>
    /// <param name="type">The competing intents; a rename conflict moves the item instead of copying content.</param>
    /// <param name="state">The item as it is now.</param>
    /// <param name="choice">A choice other than <see cref="ConflictResolution.Defer"/>.</param>
    /// <returns>The steps to plan; deleting only when the chosen side no longer has the item, which is the user's explicit decision.</returns>
    public static Resolved Plan(ConflictType type, CanonicalItemState state, ConflictResolution choice)
        => choice switch
        {
            ConflictResolution.KeepLocal => KeepLocal(type, state),
            ConflictResolution.KeepRemote => KeepRemote(type, state),
            _ => state.Local is not null && state.Remote is null ? KeepLocal(type, state) : KeepRemote(type, state)
        };

    /// <summary>Tells whether keep both must first copy the local file aside: only when both sides still have the item.</summary>
    /// <param name="state">The item as it is now.</param>
    /// <returns><see langword="true"/> when the local content would otherwise be replaced.</returns>
    public static bool NeedsCopy(CanonicalItemState state) => state.Local is not null && state.Remote is not null;

    private static Resolved KeepLocal(ConflictType type, CanonicalItemState state)
        => new([state.Local is null ? SyncDecisionType.DeleteRemote : type == ConflictType.RenameRename && state.Remote is not null ? SyncDecisionType.RenameRemote : SyncDecisionType.UpdateRemote]);

    private static Resolved KeepRemote(ConflictType type, CanonicalItemState state)
        => new([state.Remote is null ? SyncDecisionType.DeleteLocal : type == ConflictType.RenameRename && state.Local is not null ? SyncDecisionType.RenameLocal : SyncDecisionType.UpdateLocal]);
}
