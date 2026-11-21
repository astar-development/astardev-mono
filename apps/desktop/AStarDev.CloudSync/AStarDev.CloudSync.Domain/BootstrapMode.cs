namespace AStarDev.CloudSync.Domain;

/// <summary>Which side is authoritative the first time a sync job synchronises. Stored when the job is created and acted on by the initial crawl.</summary>
public enum BootstrapMode
{
    /// <summary>The remote content is authoritative and is copied to the local root.</summary>
    Download,

    /// <summary>The local content is authoritative and is copied to the remote root.</summary>
    Upload,

    /// <summary>Content on both sides is reconciled by the normal rules, and nothing is discarded without an explicit user decision.</summary>
    Merge
}
