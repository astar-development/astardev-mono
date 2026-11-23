using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Deletes telemetry older than the <see cref="SettingNames.TelemetryRetentionDays"/> setting once an hour. The first run is one interval after start, so startup never waits on it.</summary>
internal sealed class TelemetryPruningService(ITelemetryStore store, ISettingsStore settings, TimeProvider timeProvider, ILogger<TelemetryPruningService> logger) : BackgroundService
{
    private const int DefaultRetentionDays = 7;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>Prunes once using the saved retention.</summary>
    /// <param name="cancellationToken">Cancelling abandons the prune.</param>
    /// <returns>A task that completes when the prune finished or failed; failure is logged, never thrown.</returns>
    internal async Task PruneOnceAsync(CancellationToken cancellationToken)
    {
        var days = await RetentionDaysAsync(cancellationToken).ConfigureAwait(false);
        var result = await store.PruneAsync(timeProvider.GetUtcNow().AddDays(-days), cancellationToken).ConfigureAwait(false);

        switch (result.Value)
        {
            case Ok<int> ok:
                PersistenceLog.TelemetryPruned(logger, ok.Value, days);
                break;
            case Fail<StoreError> failure:
                PersistenceLog.TelemetryPruneFailed(logger, failure.Error.Value.ToString() ?? string.Empty, ErrorInfo.CodeOf(failure.Error.Value));
                break;
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) await PruneOnceAsync(stoppingToken).ConfigureAwait(false);
    }

    private async Task<int> RetentionDaysAsync(CancellationToken cancellationToken)
    {
        var saved = await settings.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return saved.Value is Ok<IReadOnlyDictionary<string, string>> ok && int.TryParse(ok.Value[SettingNames.TelemetryRetentionDays], out var days) && days >= 1 ? days : DefaultRetentionDays;
    }
}
