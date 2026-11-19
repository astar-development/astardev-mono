using AStarDev.CloudSync.Domain;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

/// <summary>
/// The only way the sync engine talks to a cloud provider. Failure is a <see cref="ProviderError"/> value, never an exception;
/// cancellation is not a failure and propagates as <see cref="OperationCanceledException"/>.
/// Later slices add the remaining members of doc 09 (deltas, download, upload, rename, delete).
/// </summary>
public interface ICloudProvider
{
    Task<Result<ProviderAccount, ProviderError>> ConnectAsync(CancellationToken cancellationToken);

    Task<Result<ProviderSnapshotPage, ProviderError>> GetSnapshotAsync(SyncScope scope, CancellationToken cancellationToken);
}
