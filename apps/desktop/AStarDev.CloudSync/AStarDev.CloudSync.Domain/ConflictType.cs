namespace AStarDev.CloudSync.Domain;

/// <summary>Which pair of competing intents made an item a conflict. Stored as the enum name.</summary>
public enum ConflictType
{
    /// <summary>Both sides created the item and the creations cannot be shown to be the same.</summary>
    CreateCreate,

    /// <summary>Both sides changed the content.</summary>
    ModifyModify,

    /// <summary>Both sides moved the item to different paths.</summary>
    RenameRename,

    /// <summary>One side deleted the item while the other renamed or modified it.</summary>
    KeepDelete
}
