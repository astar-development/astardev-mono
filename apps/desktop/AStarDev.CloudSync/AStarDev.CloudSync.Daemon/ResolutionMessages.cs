using AStarDev.CloudSync.SyncEngine;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Turns conflict resolution failures into text the UI can show to a person as it is.</summary>
internal static class ResolutionMessages
{
    /// <summary>One sentence per <see cref="ResolutionError"/> case; a store failure uses <see cref="StoreErrorMessages"/>.</summary>
    public static string Describe(ResolutionError error) => error switch
    {
        ConflictNotOpen => "A conflict is not open any more; it may already be resolved.",
        ConflictItemGone => "The file of a conflict is no longer known.",
        ConflictItemBusy => "A file still has work waiting, so its conflict was not resolved. Try again when that work is done.",
        KeepBothCopyFailed e => $"A copy of the local file could not be made, so nothing was changed: {e.Reason}",
        ResolutionStoreFailed e => StoreErrorMessages.Describe(e.Error, "resolved", "conflict")
    };
}
