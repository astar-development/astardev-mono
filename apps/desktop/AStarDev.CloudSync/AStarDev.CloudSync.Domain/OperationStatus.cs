namespace AStarDev.CloudSync.Domain;

/// <summary>Where an operation is in its life. Stored as the enum name.</summary>
public enum OperationStatus
{
    /// <summary>Waiting to be claimed, once its next attempt time has passed.</summary>
    Pending,

    /// <summary>Claimed and executing. At most one operation per item is in this state.</summary>
    Running,

    /// <summary>Finished successfully. Kept for history and never claimed again.</summary>
    Completed,

    /// <summary>The last attempt failed and no decision about a retry has been recorded yet.</summary>
    Failed,

    /// <summary>Gave up after the retry limit. Stays visible and is never claimed again.</summary>
    DeadLetter
}
