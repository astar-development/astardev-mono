namespace AStarDev.CloudSync.Domain;

/// <summary>Where a conflict is in its life. Stored as the enum name.</summary>
public enum ConflictStatus
{
    /// <summary>Waiting for the user to choose; nothing is applied to the item meanwhile.</summary>
    Open,

    /// <summary>The user chose an outcome and it was applied.</summary>
    Resolved,

    /// <summary>The user chose to leave the item as it is.</summary>
    Ignored
}
