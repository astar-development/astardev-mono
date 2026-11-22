namespace AStarDev.CloudSync.Domain;

/// <summary>How soon an operation is claimed relative to others. Declaration order is claim order, so the first value is claimed first. Stored as the number.</summary>
public enum OperationPriority
{
    /// <summary>Requested by the user, such as a manual sync or conflict resolution.</summary>
    UserInitiated,

    /// <summary>Needed for something the user is looking at.</summary>
    Interactive,

    /// <summary>Normal uploads and downloads; the default.</summary>
    Background,

    /// <summary>Housekeeping that can wait for everything else.</summary>
    Maintenance
}
