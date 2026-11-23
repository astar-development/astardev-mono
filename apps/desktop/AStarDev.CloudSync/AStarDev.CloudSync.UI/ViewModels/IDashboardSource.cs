using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Where the dashboard gets its numbers.</summary>
internal interface IDashboardSource
{
    /// <summary>Reads the current dashboard values.</summary>
    /// <param name="cancellationToken">Cancelling abandons the read and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>A complete snapshot, or the <see cref="IpcError"/> that stopped the read, for example <see cref="ConnectionFailed"/> while the daemon is stopped.</returns>
    public Task<Result<DashboardSnapshot, IpcError>> GetSnapshotAsync(CancellationToken cancellationToken);
}
