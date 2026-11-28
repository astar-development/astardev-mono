using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Deletes completed operations created more than the <see cref="SettingNames.OperationRetentionDays"/> setting ago once an hour, so the queue and history reads stay small. The first run is one interval after start, so startup never waits on it.</summary>
internal sealed class OperationPruningService(IOperationStore store, ISettingsStore settings, TimeProvider timeProvider, ILogger<OperationPruningService> logger) : BackgroundService
{
    private const int DefaultRetentionDays = 30;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>Prunes once using the saved retention.</summary>
    /// <param name="cancellationToken">Cancelling abandons the prune.</param>
    /// <returns>A task that completes when the prune finished or failed; failure is logged, never thrown.</returns>
    internal async Task PruneOnceAsync(CancellationToken cancellationToken)
    {
        var days = await RetentionDaysAsync(cancellationToken).ConfigureAwait(false);
        var result = await store.PruneCompletedAsync(timeProvider.GetUtcNow().AddDays(-days), cancellationToken).ConfigureAwait(false);

        switch (result.Value)
        {
            case Ok<int> ok:
                PersistenceLog.OperationsPruned(logger, ok.Value, days);
                break;
            case Fail<StoreError> failure:
                PersistenceLog.OperationPruneFailed(logger, failure.Error.Value.ToString() ?? string.Empty, ErrorInfo.CodeOf(failure.Error.Value));
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

        return saved.Value is Ok<IReadOnlyDictionary<string, string>> ok && int.TryParse(ok.Value[SettingNames.OperationRetentionDays], out var days) && days >= 1 ? days : DefaultRetentionDays;
    }
}
