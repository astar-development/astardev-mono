namespace AStarDev.CloudSync.Domain;

/// <summary>Where a conflict is in its life. Stored as the enum name.</summary>
public enum ConflictStatus
{
    /// <summary>Waiting for the user to choose; nothing is applied to the item meanwhile.</summary>
    Open,

    /// <summary>The user chose an outcome and it was applied.</summary>
    Resolved,

    /// <summary>The user chose to leave the item as it is.</summary>
    Ignored,

    /// <summary>The user postponed the decision. Still unresolved: it is listed with the open conflicts, can be resolved later and counts as an existing conflict, so planning again does not raise a second one.</summary>
    Deferred
}
