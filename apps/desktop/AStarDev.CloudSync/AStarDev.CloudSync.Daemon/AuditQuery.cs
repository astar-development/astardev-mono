using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetAudit"/> from the <see cref="IEventJournal"/>, enforcing the audit retention first.</summary>
internal sealed class AuditQuery(IEventJournal journal, ISettingsStore settings, TimeProvider timeProvider)
{
    /// <summary>Prunes entries older than <see cref="SettingNames.AuditRetentionDays"/>, then reads the requested page of the entries in the range, so one answer fits an IPC frame.</summary>
    /// <returns>The page, newest first, with how many entries match; a negative <see cref="GetAudit.Skip"/> counts as zero and a <see cref="GetAudit.Take"/> outside 1 to <see cref="ApplicationConstants.ListLimit"/> is brought into that range. <see cref="HandlerFailed"/> carries the store's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<AuditView, IpcError>> HandleAsync(GetAudit query, CancellationToken cancellationToken)
        => await PruneAsync(cancellationToken) switch
        {
            Ok<int> => await ReadPageAsync(query, cancellationToken),
            Fail<StoreError> fail => Failed(fail.Error)
        };

    /// <summary>Prunes like <see cref="HandleAsync"/>, then lists every entry in the range, for an export that is written to a file and never sent as one IPC frame.</summary>
    /// <returns>Every entry, newest first like the pages of <see cref="HandleAsync"/>, or the same failures as <see cref="HandleAsync"/>.</returns>
    public async Task<Result<IReadOnlyList<AuditEntryView>, IpcError>> ListAllAsync(GetAudit query, CancellationToken cancellationToken)
        => await PruneAsync(cancellationToken) switch
        {
            Ok<int> => await ReadAllAsync(query, cancellationToken),
            Fail<StoreError> fail => Failed(fail.Error)
        };

    private static Fail<IpcError> Failed(StoreError error) => new(new HandlerFailed(StoreErrorMessages.Describe(error, "read", "audit trail")));

    private static AuditEntryView View(JournalEntry entry) => new(entry.Id, entry.OccurredUtc, entry.EventType, entry.AggregateId, entry.PayloadJson);

    private async Task<Result<int, StoreError>> PruneAsync(CancellationToken cancellationToken)
        => await settings.GetAllAsync(cancellationToken) switch
        {
            Ok<IReadOnlyDictionary<string, string>> all => await journal.PruneAsync(timeProvider.GetUtcNow().AddDays(-int.Parse(all.Value[SettingNames.AuditRetentionDays])), cancellationToken),
            Fail<StoreError> fail => fail
        };

    private async Task<Result<AuditView, IpcError>> ReadPageAsync(GetAudit query, CancellationToken cancellationToken)
        => await journal.PageAsync(query.FromUtc, query.ToUtc, query.EventTypes, Paging.Skip(query.Skip), Paging.Take(query.Take), cancellationToken) switch
        {
            Ok<Page<JournalEntry>> page => new Ok<AuditView>(new AuditView(page.Value.Total, [.. page.Value.Items.Select(View)])),
            Fail<StoreError> fail => Failed(fail.Error)
        };

    private async Task<Result<IReadOnlyList<AuditEntryView>, IpcError>> ReadAllAsync(GetAudit query, CancellationToken cancellationToken)
        => await journal.ListAsync(query.FromUtc, query.ToUtc, query.EventTypes, cancellationToken) switch
        {
            Ok<IReadOnlyList<JournalEntry>> entries => new Ok<IReadOnlyList<AuditEntryView>>([.. entries.Value.Reverse().Select(View)]),
            Fail<StoreError> fail => Failed(fail.Error)
        };
}
