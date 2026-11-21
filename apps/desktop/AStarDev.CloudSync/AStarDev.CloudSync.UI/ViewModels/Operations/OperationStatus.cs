namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>Where an operation is in its life. Each value has a <c>Operations.Status.&lt;Value&gt;</c> text.</summary>
internal enum OperationStatus
{
    /// <summary>Waiting in the queue; not started.</summary>
    Pending,

    /// <summary>Executing now.</summary>
    Running,

    /// <summary>Finished successfully.</summary>
    Completed,

    /// <summary>Finished unsuccessfully; the user may need to act.</summary>
    Failed
}
