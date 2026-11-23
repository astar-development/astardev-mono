using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetTraces"/> from the <see cref="ITelemetryStore"/>.</summary>
internal sealed class TracesQuery(ITelemetryStore store)
{
    /// <summary>Reads the stored spans that match the filters.</summary>
    /// <returns>The spans newest first, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<TracesView, IpcError>> HandleAsync(GetTraces query, CancellationToken cancellationToken)
        => await store.QueryAsync(new TelemetryQuery(TelemetryKind.Span) { Since = query.Since, Text = query.TraceId }, cancellationToken) switch
        {
            Ok<IReadOnlyList<TelemetryEntry>> entries => new Ok<TracesView>(new TracesView([.. SpanText.ToViews(entries.Value)])),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read", "traces")))
        };
}
