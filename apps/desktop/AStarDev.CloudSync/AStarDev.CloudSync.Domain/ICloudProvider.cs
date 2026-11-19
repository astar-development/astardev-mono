namespace AStarDev.CloudSync.Domain;

/// <summary>
/// The only way the sync engine talks to a cloud provider. Later slices add the
/// remaining members of doc 09 (deltas, download, upload, rename, delete).
/// </summary>
public interface ICloudProvider
{
    Task<ProviderAccount> ConnectAsync();

    Task<ProviderSnapshotPage> GetSnapshotAsync(SyncScope scope, CancellationToken cancellationToken);
}
