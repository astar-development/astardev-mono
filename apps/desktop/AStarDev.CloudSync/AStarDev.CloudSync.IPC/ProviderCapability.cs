namespace AStarDev.CloudSync.IPC;

/// <summary>A feature a provider offers, as the daemon reports it. The UI shows or hides actions from these values instead of asking which provider it is.</summary>
public enum ProviderCapability
{
    /// <summary>The user can choose which remote folders are synchronised.</summary>
    SelectiveSync,

    /// <summary>The provider reports only what changed since the last sync, so syncing can be paused and resumed cheaply.</summary>
    DeltaSync,

    /// <summary>The provider keeps earlier versions of files.</summary>
    Versioning,

    /// <summary>Deleted files can be restored from the provider's recycle bin.</summary>
    RecycleBin,

    /// <summary>The provider supports content shared with other people.</summary>
    SharedContent
}
