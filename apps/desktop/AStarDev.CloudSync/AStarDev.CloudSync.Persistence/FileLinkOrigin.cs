namespace AStarDev.CloudSync.Persistence;

/// <summary>Who created a link between an item and a term.</summary>
internal enum FileLinkOrigin
{
    /// <summary>Classification created it; the next run replaces it.</summary>
    Auto,

    /// <summary>The user chose it; classification never removes it.</summary>
    User
}
