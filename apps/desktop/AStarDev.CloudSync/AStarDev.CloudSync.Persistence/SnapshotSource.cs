namespace AStarDev.CloudSync.Persistence;

/// <summary>Which side of the sync an observation was taken from.</summary>
internal enum SnapshotSource
{
    /// <summary>Observed on the local disk.</summary>
    Local,

    /// <summary>Reported by the cloud provider.</summary>
    Remote
}
