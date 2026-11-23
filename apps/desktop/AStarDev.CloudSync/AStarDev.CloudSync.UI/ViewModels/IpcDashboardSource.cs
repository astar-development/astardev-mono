using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Reads the dashboard from the daemon over IPC.</summary>
/// <param name="createClient">Creates a fresh client per read; one client is one connection.</param>
internal sealed class IpcDashboardSource(Func<IIpcClient> createClient) : IDashboardSource
{
    /// <inheritdoc/>
    /// <remarks>Connects, asks with <see cref="GetDashboardSnapshot"/> and disconnects, so each call stands alone.</remarks>
    public async Task<Result<DashboardSnapshot, IpcError>> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        return await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> => await QueryAsync(client, cancellationToken),
            Fail<IpcError> fail => fail
        };
    }

    private static async Task<Result<DashboardSnapshot, IpcError>> QueryAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetDashboardSnapshot(), cancellationToken) switch
        {
            Ok<DashboardView> view => new Ok<DashboardSnapshot>(DashboardSnapshot.From(view.Value)),
            Fail<IpcError> fail => fail
        };
}
