namespace AStarDev.CloudSync.SyncEngine;

/// <summary>What the user decided for a conflict. The name is journaled and announced as the resolution text.</summary>
public enum ConflictResolution
{
    /// <summary>The local side wins: the local file is uploaded, or the remote file is deleted when the local one is gone.</summary>
    KeepLocal,

    /// <summary>The remote side wins: the remote file is downloaded over the local one, or the local file is deleted when the remote one is gone.</summary>
    KeepRemote,

    /// <summary>Nothing is discarded: the local file is first copied to a conflict-named file that syncs as a new item, then the remote version is applied. When only one side still has the file, that side is kept.</summary>
    KeepBoth,

    /// <summary>The user is not deciding yet: the conflict is marked deferred, stays listed and can still be resolved later; nothing about the item changes.</summary>
    Defer
}
