using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetRemoteRootSummary"/> by listing the drive root through <see cref="ICloudProvider"/>.</summary>
internal sealed class RootSummaryQuery(ICloudProvider provider)
{
    /// <summary>Lists the root and counts what is directly under it.</summary>
    /// <returns>The count, or <see cref="HandlerFailed"/> carrying the provider's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<RemoteRootSummaryView, IpcError>> HandleAsync(GetRemoteRootSummary query, CancellationToken cancellationToken)
        => await provider.GetSnapshotAsync(new RemoteItemId(ApplicationConstants.DriveRootItemId), null, cancellationToken) switch
        {
            Ok<ProviderSnapshotPage> page => new Ok<RemoteRootSummaryView>(new RemoteRootSummaryView(page.Value.Items.Count)),
            Fail<ProviderError> fail => new Fail<IpcError>(new HandlerFailed(ProviderErrorMessages.Describe(fail.Error)))
        };
}
