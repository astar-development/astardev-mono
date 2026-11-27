using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetLogs"/> from the <see cref="ITelemetryStore"/>.</summary>
internal sealed class LogsQuery(ITelemetryStore store)
{
    /// <summary>Reads the stored log entries that match the filters, each message cut to <see cref="ApplicationConstants.LogMessageLimit"/> characters, so one answer fits an IPC frame.</summary>
    /// <returns>The entries newest first, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<LogsView, IpcError>> HandleAsync(GetLogs query, CancellationToken cancellationToken)
        => await store.QueryAsync(new TelemetryQuery(TelemetryKind.Log) { Since = query.Since, MinimumLevel = query.MinimumLevel, Text = query.Text }, cancellationToken) switch
        {
            Ok<IReadOnlyList<TelemetryEntry>> entries => new Ok<LogsView>(new LogsView([.. entries.Value.Select(entry => new LogEntryView(entry.Timestamp, entry.Level, entry.Source, Truncated(entry.Text)))])),
            Fail<StoreError> fail => new Fail<IpcError>(new HandlerFailed(StoreErrorMessages.Describe(fail.Error, "read", "logs")))
        };

    private static string Truncated(string text) => text.Length > ApplicationConstants.LogMessageLimit ? text[..ApplicationConstants.LogMessageLimit] : text;
}
