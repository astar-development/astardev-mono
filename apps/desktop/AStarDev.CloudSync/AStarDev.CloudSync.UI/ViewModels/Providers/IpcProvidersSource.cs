using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>Holds the last <see cref="ProvidersView"/> the daemon sent, which <see cref="IpcProviderCatalogue"/> and <see cref="IpcProviderInsightsSource"/> both read, so one question serves both.</summary>
/// <param name="createClient">Creates a fresh client per read; one client is one connection.</param>
internal sealed class IpcProvidersSource(Func<IIpcClient> createClient)
{
    /// <summary>What the daemon last reported; no providers and no accounts until a read succeeds. A failed read leaves it as it was.</summary>
    public ProvidersView Current { get; private set; } = new([], []);

    /// <summary>Connects, asks with <see cref="GetProviders"/> and disconnects, so each call stands alone.</summary>
    /// <param name="cancellationToken">Cancelling abandons the read and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>Success once <see cref="Current"/> is replaced, or the reason the daemon could not be asked.</returns>
    public async Task<Result<Unit, IpcError>> RefreshAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        return await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> => await QueryAsync(client, cancellationToken),
            Fail<IpcError> fail => fail
        };
    }

    private async Task<Result<Unit, IpcError>> QueryAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetProviders(), cancellationToken) switch
        {
            Ok<ProvidersView> view => Keep(view.Value),
            Fail<IpcError> fail => fail
        };

    private Ok<Unit> Keep(ProvidersView view)
    {
        Current = view;

        return new Ok<Unit>(default);
    }
}
