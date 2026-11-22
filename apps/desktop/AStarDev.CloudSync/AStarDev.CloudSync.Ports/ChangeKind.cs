namespace AStarDev.CloudSync.Ports;

/// <summary>What happened to a remote item since a delta token was issued.</summary>
public enum ChangeKind
{
    /// <summary>The item appeared. <see cref="ProviderChange.Item"/> is set.</summary>
    Created,

    /// <summary>The item's content or metadata changed. <see cref="ProviderChange.Item"/> is set.</summary>
    Modified,

    /// <summary>The item is gone. <see cref="ProviderChange.Item"/> is <see langword="null"/>.</summary>
    Deleted,

    /// <summary>The item changed name or folder. <see cref="ProviderChange.Item"/> holds the new path; <see cref="ProviderChange.PreviousPath"/> the old one when the provider knows it.</summary>
    Renamed
}
