using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>Each account's own last sync, storage and activity, as the daemon last reported them.</summary>
/// <param name="source">Holds what the daemon reported at the last refresh.</param>
/// <param name="localisation">Supplies the texts.</param>
/// <param name="culture">Formats times and numbers.</param>
internal sealed class IpcProviderInsightsSource(IpcProvidersSource source, ILocalisationService localisation, ICultureService culture) : IProviderInsightsSource
{
    private const double BytesPerGigabyte = 1_000_000_000d;

    /// <inheritdoc/>
    public Task<Result<Unit, IpcError>> RefreshAsync(CancellationToken cancellationToken) => source.RefreshAsync(cancellationToken);

    /// <inheritdoc/>
    /// <remarks>An account the daemon has not reported yet shows as never synced, storage unavailable and no activity.</remarks>
    public ProviderInsights Describe(AccountView account)
    {
        var figures = source.Current.Accounts.Where(candidate => candidate.AccountId == account.Id).Concat([new ProviderInsightsView(account.Id, null, null, null, 0, 0, 0)]).First();

        return new ProviderInsights(HealthOf(figures), LastSync(figures), Storage(figures), Activity(figures));
    }

    private static ProviderHealth HealthOf(ProviderInsightsView figures)
    {
        if (figures.Failed > 0) return ProviderHealth.Warning;

        return figures.Running > 0 ? ProviderHealth.Syncing : ProviderHealth.Healthy;
    }

    private string LastSync(ProviderInsightsView figures)
        => figures.LastSyncUtc is { } synced
            ? localisation.Format("Providers.Card.LastSync", ("date", culture.FormatDate(synced.ToLocalTime())), ("time", culture.FormatTime(synced.ToLocalTime())))
            : localisation.Get("Providers.Card.LastSync.Never");

    private string Storage(ProviderInsightsView figures)
        => figures is { StorageUsedBytes: { } used, StorageTotalBytes: { } total }
            ? localisation.Format("Providers.Card.Storage", ("used", Gigabytes(used)), ("total", Gigabytes(total)))
            : localisation.Get("Providers.Card.Storage.Unavailable");

    private string Gigabytes(long bytes) => culture.FormatValue(Math.Round(bytes / BytesPerGigabyte, 1));

    private string Activity(ProviderInsightsView figures)
        => localisation.Format("Providers.Card.Activity", ("pending", figures.Pending), ("running", figures.Running), ("failed", figures.Failed), ("completed", figures.Completed));
}
