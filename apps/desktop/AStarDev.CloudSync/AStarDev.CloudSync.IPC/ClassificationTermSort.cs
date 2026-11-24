namespace AStarDev.CloudSync.IPC;

/// <summary>What <see cref="GetClassificationTerms"/> orders the terms by.</summary>
public enum ClassificationTermSort
{
    /// <summary>The term text, ignoring case.</summary>
    Text,

    /// <summary>The category name, then the term text.</summary>
    Category,

    /// <summary>The number of files linked to the term, then the term text.</summary>
    Files
}
