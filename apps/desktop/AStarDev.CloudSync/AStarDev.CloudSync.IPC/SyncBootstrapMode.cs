namespace AStarDev.CloudSync.IPC;

/// <summary>Which side is authoritative the first time a sync job synchronises, as the UI chooses it and the daemon stores it.</summary>
public enum SyncBootstrapMode
{
    /// <summary>The remote content is authoritative and is copied to the local folder.</summary>
    Download,

    /// <summary>The local content is authoritative and is copied to the remote folders.</summary>
    Upload,

    /// <summary>Content on both sides is reconciled, and nothing is discarded without an explicit user decision.</summary>
    Merge
}
