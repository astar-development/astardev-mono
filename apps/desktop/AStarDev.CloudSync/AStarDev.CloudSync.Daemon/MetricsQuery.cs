using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetMetrics"/> from the <see cref="ITelemetryStore"/>.</summary>
internal sealed class MetricsQuery(ITelemetryStore store)
{
    private const int SampleLimit = 2000;

    /// <summary>Reads the stored samples that match the filters.</summary>
    /// <returns>The samples newest first, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<MetricsView, IpcError>> HandleAsync(GetMetrics query, CancellationToken cancellationToken)
        => await store.QueryAsync(new TelemetryQuery(TelemetryKind.Metric) { Since = query.Since, Limit = SampleLimit }, cancellationToken) switch
        {
            Ok<IReadOnlyList<TelemetryEntry>> entries => new Ok<MetricsView>(new MetricsView([.. MetricText.ToViews(entries.Value).Where(sample => query.Name.Length == 0 || sample.Name == query.Name)])),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read", "metrics")))
        };
}
