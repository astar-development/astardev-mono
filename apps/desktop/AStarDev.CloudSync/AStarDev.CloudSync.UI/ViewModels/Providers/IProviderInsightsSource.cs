using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>Supplies the figures a card shows beyond what the account list carries.</summary>
internal interface IProviderInsightsSource
{
    /// <summary>Loads the latest figures, and with them whatever the matching <see cref="IProviderCatalogue"/> reads. Call before <see cref="Describe"/> to show current values.</summary>
    /// <param name="cancellationToken">Cancelling abandons the load and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>Success, or the reason the daemon could not be asked, in which case the earlier figures stay.</returns>
    Task<Result<Unit, IpcError>> RefreshAsync(CancellationToken cancellationToken);

    /// <summary>The figures for <paramref name="account"/> as last loaded. Never fails: unknown values come back as readable text.</summary>
    /// <param name="account">The account the card is for.</param>
    ProviderInsights Describe(AccountView account);
}
