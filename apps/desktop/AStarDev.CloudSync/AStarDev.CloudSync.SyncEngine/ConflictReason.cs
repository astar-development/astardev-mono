namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Which pair of competing intents made a decision a conflict.</summary>
internal enum ConflictReason
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
