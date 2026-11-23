namespace AStarDev.CloudSync.Ports;

/// <summary>A feature a provider offers, which decides what the UI lets the user do with its accounts.</summary>
public enum ProviderFeature
{
    /// <summary>The user can choose which remote folders are synchronised.</summary>
    SelectiveSync,

    /// <summary>The provider reports only what changed since the last sync.</summary>
    DeltaSync,

    /// <summary>The provider keeps earlier versions of files.</summary>
    Versioning,

    /// <summary>Deleted files can be restored from the provider's recycle bin.</summary>
    RecycleBin,

    /// <summary>The provider supports content shared with other people.</summary>
    SharedContent
}
