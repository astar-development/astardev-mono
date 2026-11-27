using System.Globalization;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="ExportHistory"/> and <see cref="ExportAudit"/> by running the screen's own query and writing its rows as CSV.</summary>
internal sealed class ExportQuery(HistoryQuery history, AuditQuery audit)
{
    private static readonly string[] HistoryHeader = ["OperationId", "Timestamp", "Provider", "Type", "Status", "Path", "SizeBytes"];
    private static readonly string[] AuditHeader = ["Id", "OccurredUtc", "EventType", "AggregateId", "Details"];

    /// <summary>Exports the operations <see cref="HistoryQuery"/> lists for the same filter, newest first, with the columns <c>OperationId, Timestamp, Provider, Type, Status, Path, SizeBytes</c>.</summary>
    /// <returns>The CSV, or the failure <see cref="HistoryQuery"/> gave. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ExportView, IpcError>> HandleAsync(ExportHistory query, CancellationToken cancellationToken)
        => await history.ListAllAsync(query.Filter, cancellationToken) switch
        {
            Ok<HistoryView> ok => new Ok<ExportView>(new ExportView(CsvRows.Write(HistoryHeader, ok.Value.Entries.Select(entry => (IReadOnlyList<string>)[entry.OperationId.ToString(), Stamp(entry.Timestamp), entry.Provider, entry.Type, entry.Status, entry.Path, entry.SizeBytes.ToString(CultureInfo.InvariantCulture)])))),
            Fail<IpcError> fail => fail
        };

    /// <summary>Exports the entries <see cref="AuditQuery"/> lists for the same filter, oldest first, with the columns <c>Id, OccurredUtc, EventType, AggregateId, Details</c>.</summary>
    /// <returns>The CSV, or the failure <see cref="AuditQuery"/> gave. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<ExportView, IpcError>> HandleAsync(ExportAudit query, CancellationToken cancellationToken)
        => await audit.HandleAsync(query.Filter, cancellationToken) switch
        {
            Ok<AuditView> ok => new Ok<ExportView>(new ExportView(CsvRows.Write(AuditHeader, ok.Value.Entries.Select(entry => (IReadOnlyList<string>)[entry.Id.ToString(), Stamp(entry.OccurredUtc), entry.EventType, entry.AggregateId.ToString(), entry.DetailsJson])))),
            Fail<IpcError> fail => fail
        };

    private static string Stamp(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
