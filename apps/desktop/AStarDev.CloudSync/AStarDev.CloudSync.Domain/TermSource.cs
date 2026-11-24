namespace AStarDev.CloudSync.Domain;

/// <summary>Where a classification term came from. Decides how sure a match on it is.</summary>
public enum TermSource
{
    /// <summary>Shipped with the app.</summary>
    Seed,

    /// <summary>Added or promoted by the user.</summary>
    User,

    /// <summary>Derived automatically from files.</summary>
    Auto,

    /// <summary>Found in file names and waiting for review. A match on it is low confidence.</summary>
    Suggested
}
