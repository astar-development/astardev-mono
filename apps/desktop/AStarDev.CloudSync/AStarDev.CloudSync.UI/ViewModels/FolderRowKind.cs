namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>What a row of the folder tree stands for.</summary>
public enum FolderRowKind
{
    /// <summary>A remote folder. Expanding it lists the folders inside.</summary>
    Folder,

    /// <summary>A placeholder shown while the folders inside a node are being listed, and before the first expansion so the node can be expanded.</summary>
    Loading,

    /// <summary>The reason listing failed, worded for the user. Collapse and expand the parent node to try again.</summary>
    Error
}
