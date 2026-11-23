using System.Text.Json;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Records the significant actions of the user in the <see cref="IEventJournal"/>, which is the audit trail.</summary>
internal sealed partial class AuditJournal(IEventJournal journal, TimeProvider timeProvider, ILogger<AuditJournal> logger)
{
    /// <summary>Appends one entry stamped with the current time. A failure to record is logged and never fails the action that was already done.</summary>
    /// <param name="eventType">The kind of action, for example <c>SyncScopeAdded</c>.</param>
    /// <param name="aggregateId">What the action concerns.</param>
    /// <param name="details">Serialised to the entry's JSON details.</param>
    /// <param name="cancellationToken">Cancelling before the write completes records nothing and surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task RecordAsync(string eventType, Guid aggregateId, object details, CancellationToken cancellationToken)
    {
        var entry = new JournalEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), eventType, aggregateId, JsonSerializer.Serialize(details));
        if (await journal.AppendAsync(entry, cancellationToken) is { Value: Fail<StoreError> fail }) RecordingFailed(logger, eventType, StoreErrorMessages.Describe(fail.Error, "recorded", "audit entry"));
    }

    [LoggerMessage(EventId = 6001, Level = LogLevel.Warning, Message = "Recording audit entry {EventType} failed: {Message}")]
    private static partial void RecordingFailed(ILogger logger, string eventType, string message);
}
