namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>What an operation did to a file or folder. Each value has a <c>Operations.Type.&lt;Value&gt;</c> text.</summary>
internal enum OperationType
{
    /// <summary>A remote item was copied to the local folder.</summary>
    Download,

    /// <summary>A local item was copied to the remote folder.</summary>
    Upload,

    /// <summary>An item changed name or location.</summary>
    Rename,

    /// <summary>An item was removed.</summary>
    Delete,

    /// <summary>Both sides changed an item, so a conflict was recorded. Resolving it happens on the Conflicts screen.</summary>
    ConflictCreated,

    /// <summary>A batch of queued operations was executed.</summary>
    QueueRun
}
