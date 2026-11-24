namespace AStarDev.CloudSync.Domain;

/// <summary>Whether a classification term takes part in classifying files.</summary>
public enum TermStatus
{
    /// <summary>The term classifies files.</summary>
    Active,

    /// <summary>The user ignored the term. It is kept, never deleted, so it cannot come back from a later sync or seed.</summary>
    Ignored
}
