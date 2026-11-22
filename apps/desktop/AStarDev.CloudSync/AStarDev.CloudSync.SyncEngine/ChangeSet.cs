namespace AStarDev.CloudSync.SyncEngine;

/// <summary>How one side differs from the known baseline. Combine flags when a side changed several things.</summary>
[Flags]
internal enum ChangeSet
{
    /// <summary>Same as the baseline.</summary>
    None = 0,

    /// <summary>Exists although the baseline has no such item.</summary>
    Created = 1 << 0,

    /// <summary>Missing although the baseline has the item.</summary>
    Deleted = 1 << 1,

    /// <summary>Sits at a different path than the baseline.</summary>
    Renamed = 1 << 2,

    /// <summary>Has different content than the baseline.</summary>
    Modified = 1 << 3
}
