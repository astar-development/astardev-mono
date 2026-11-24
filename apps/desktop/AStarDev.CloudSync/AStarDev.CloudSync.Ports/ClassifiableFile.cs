using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>A stored file that classification can look at. Folders are never listed.</summary>
/// <param name="ItemId">Our identity for the file.</param>
/// <param name="SyncJobId">The job the file belongs to.</param>
/// <param name="RemoteItemId">The provider's identity for the file.</param>
/// <param name="Path">Where the file sits now below the job's local root.</param>
public sealed record ClassifiableFile(ItemId ItemId, SyncJobId SyncJobId, RemoteItemId RemoteItemId, RelativePath Path);
