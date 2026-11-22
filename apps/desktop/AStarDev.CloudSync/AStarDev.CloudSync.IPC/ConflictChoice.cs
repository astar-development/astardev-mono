namespace AStarDev.CloudSync.IPC;

/// <summary>What the user decided for a conflict, as the UI sends it in <see cref="ResolveConflict"/>.</summary>
public enum ConflictChoice
{
    /// <summary>The local version wins and is uploaded; when the local file is gone, the remote file is deleted.</summary>
    KeepLocal,

    /// <summary>The remote version wins and is downloaded over the local one; when the remote file is gone, the local file is deleted.</summary>
    KeepRemote,

    /// <summary>Both versions survive: the local version is copied to a conflict-named file, then the remote version is applied. The safest choice.</summary>
    KeepBoth,

    /// <summary>Decide later: the conflict stays in <see cref="GetConflicts"/> with status <c>Deferred</c>, can still be resolved, and nothing about the file changes.</summary>
    Defer
}
