namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The kind of action the reconciler asks for. The side named in the value is the side that changes.</summary>
internal enum SyncDecisionType
{
    /// <summary>Nothing to do.</summary>
    NoOp,

    /// <summary>Make the item locally from the remote one.</summary>
    CreateLocal,

    /// <summary>Make the item remotely from the local one.</summary>
    CreateRemote,

    /// <summary>Replace the local content with the remote content.</summary>
    UpdateLocal,

    /// <summary>Replace the remote content with the local content.</summary>
    UpdateRemote,

    /// <summary>Move the local item to the remote path.</summary>
    RenameLocal,

    /// <summary>Move the remote item to the local path.</summary>
    RenameRemote,

    /// <summary>Remove the local item because the remote one is gone.</summary>
    DeleteLocal,

    /// <summary>Remove the remote item because the local one is gone.</summary>
    DeleteRemote,

    /// <summary>One side renamed the item and the other side changed its content. The planner recovers the rename and the content update from the <see cref="AStarDev.CloudSync.Domain.CanonicalItemState"/> and the per-side change sets.</summary>
    Merge,

    /// <summary>No single safe outcome exists; the user must choose.</summary>
    Conflict
}
