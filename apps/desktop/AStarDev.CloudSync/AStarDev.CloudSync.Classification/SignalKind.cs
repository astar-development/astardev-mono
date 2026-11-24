namespace AStarDev.CloudSync.Classification;

/// <summary>Which signal produced a candidate.</summary>
internal enum SignalKind
{
    /// <summary>The file extension.</summary>
    Extension,

    /// <summary>A folder name in the path.</summary>
    FolderName,

    /// <summary>A word or phrase in the file name.</summary>
    Vocabulary
}
