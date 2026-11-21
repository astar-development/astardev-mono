namespace AStarDev.CloudSync.UI.Conflicts;

/// <summary>Where a conflict is in its life, as far as the user is concerned.</summary>
internal enum ConflictStatus
{
    /// <summary>Nobody has decided yet.</summary>
    Unresolved,

    /// <summary>The user chose to decide later.</summary>
    Deferred
}
