namespace AStarDev.CloudSync.Domain;

/// <summary>What an operation does to its item. Stored as the enum name.</summary>
public enum OperationType
{
    /// <summary>Sends the local content to the remote side.</summary>
    UploadFile,

    /// <summary>Copies the remote content to the local side.</summary>
    DownloadFile,

    /// <summary>Renames or moves the item on the remote side.</summary>
    RenameRemote,

    /// <summary>Renames or moves the item on the local side.</summary>
    RenameLocal,

    /// <summary>Deletes the item on the remote side.</summary>
    DeleteRemote,

    /// <summary>Deletes the item on the local side.</summary>
    DeleteLocal
}
