using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetAudit"/> from the <see cref="IEventJournal"/>, enforcing the audit retention first.</summary>
internal sealed class AuditQuery(IEventJournal journal, ISettingsStore settings, TimeProvider timeProvider)
{
    /// <summary>Prunes entries older than <see cref="SettingNames.AuditRetentionDays"/>, then lists the newest entries in the range, at most <see cref="ApplicationConstants.ListLimit"/>, so one answer fits an IPC frame.</summary>
    /// <returns>The entries, oldest first, or <see cref="HandlerFailed"/> carrying the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public Task<Result<AuditView, IpcError>> HandleAsync(GetAudit query, CancellationToken cancellationToken) => ListAsync(query, ApplicationConstants.ListLimit, cancellationToken);

    /// <summary>Prunes and lists like <see cref="HandleAsync"/> without its limit, for an export that is written to a file and never sent as one IPC frame.</summary>
    /// <returns>The same results and failures as <see cref="HandleAsync"/>.</returns>
    public Task<Result<AuditView, IpcError>> ListAllAsync(GetAudit query, CancellationToken cancellationToken) => ListAsync(query, int.MaxValue, cancellationToken);

    private async Task<Result<AuditView, IpcError>> ListAsync(GetAudit query, int limit, CancellationToken cancellationToken)
        => await PruneAsync(cancellationToken) switch
        {
            Ok<int> => await ReadAsync(query, limit, cancellationToken),
            Fail<StoreError> fail => Failed(fail.Error)
        };

    private static Fail<IpcError> Failed(StoreError error) => new(new HandlerFailed(StoreErrorMessages.Describe(error, "read", "audit trail")));

    private async Task<Result<int, StoreError>> PruneAsync(CancellationToken cancellationToken)
        => await settings.GetAllAsync(cancellationToken) switch
        {
            Ok<IReadOnlyDictionary<string, string>> all => await journal.PruneAsync(timeProvider.GetUtcNow().AddDays(-int.Parse(all.Value[SettingNames.AuditRetentionDays])), cancellationToken),
            Fail<StoreError> fail => fail
        };

    private async Task<Result<AuditView, IpcError>> ReadAsync(GetAudit query, int limit, CancellationToken cancellationToken)
        => await journal.ListAsync(query.FromUtc, query.ToUtc, query.EventTypes, cancellationToken) switch
        {
            Ok<IReadOnlyList<JournalEntry>> entries => new Ok<AuditView>(new AuditView([.. entries.Value.TakeLast(limit).Select(entry => new AuditEntryView(entry.Id, entry.OccurredUtc, entry.EventType, entry.AggregateId, entry.PayloadJson))])),
            Fail<StoreError> fail => Failed(fail.Error)
        };
}
