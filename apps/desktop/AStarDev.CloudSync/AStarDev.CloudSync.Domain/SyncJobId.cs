namespace AStarDev.CloudSync.Domain;

/// <summary>Our own identity for a configured sync relationship between a local root and a remote root.</summary>
/// <param name="Value">Unique per sync job.</param>
public readonly record struct SyncJobId(Guid Value);
