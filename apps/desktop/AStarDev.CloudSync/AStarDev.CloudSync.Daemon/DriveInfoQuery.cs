using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetDriveInfo"/> by asking <see cref="ICloudProvider"/> for the drive facts.</summary>
internal sealed class DriveInfoQuery(ICloudProvider provider)
{
    /// <summary>Reads the storage figures from the provider.</summary>
    /// <returns>The figures, or <see cref="HandlerFailed"/> carrying the provider's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<DriveInfoView, IpcError>> HandleAsync(GetDriveInfo query, CancellationToken cancellationToken)
        => await provider.GetDriveInfoAsync(cancellationToken) switch
        {
            Ok<ProviderDriveInfo> info => new Ok<DriveInfoView>(ViewOf(info.Value)),
            Fail<ProviderError> fail => new Fail<IpcError>(new HandlerFailed(ProviderErrorMessages.Describe(fail.Error)))
        };

    private static DriveInfoView ViewOf(ProviderDriveInfo info)
        => new(info.UsedBytes, info.RemainingBytes, info.TotalBytes, info.ItemCountEstimate.Match(count => (long?)count, () => null));
}
