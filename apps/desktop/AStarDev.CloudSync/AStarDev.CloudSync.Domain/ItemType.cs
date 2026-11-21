namespace AStarDev.CloudSync.Domain;

/// <summary>Whether an item is a file or a folder. Decides whether it has content to transfer or children to traverse.</summary>
public enum ItemType
{
    /// <summary>Has content to transfer.</summary>
    File,

    /// <summary>Has children and no content of its own.</summary>
    Folder
}
