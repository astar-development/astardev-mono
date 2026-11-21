namespace AStarDev.CloudSync.UI.Conflicts;

/// <summary>The ways a conflict can be resolved. The set is owned by document 08 and slice S21; do not add options without changing them.</summary>
internal enum ConflictResolution
{
    /// <summary>The local version wins. The remote version stays recoverable.</summary>
    KeepLocal,

    /// <summary>The remote version wins. The local version stays recoverable.</summary>
    KeepRemote,

    /// <summary>Both versions are kept, the second under a conflict-dated name.</summary>
    KeepBoth,

    /// <summary>The decision is postponed; the conflict stays in the list marked as deferred.</summary>
    Defer
}
