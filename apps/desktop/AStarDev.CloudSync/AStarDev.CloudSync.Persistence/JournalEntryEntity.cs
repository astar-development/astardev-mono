using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.Persistence;

/// <summary>The stored form of a journal entry. It has no foreign key, so the journal outlives the jobs and items it describes.</summary>
internal sealed class JournalEntryEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>When the occurrence happened; always stored in UTC so stored values compare by time.</summary>
    public DateTimeOffset OccurredUtc { get; set; }

    /// <summary>The kind of occurrence.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The thing the occurrence is about.</summary>
    public Guid AggregateId { get; set; }

    /// <summary>Diagnostic details as JSON text.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>Copies the entry into its stored form, converting the time to UTC.</summary>
    /// <param name="entry">The entry to store.</param>
    /// <returns>A new, untracked entity.</returns>
    public static JournalEntryEntity From(JournalEntry entry)
        => new() { Id = entry.Id, OccurredUtc = entry.OccurredUtc.ToUniversalTime(), EventType = entry.EventType, AggregateId = entry.AggregateId, PayloadJson = entry.PayloadJson };

    /// <summary>Builds the entry from the stored row.</summary>
    /// <returns>An immutable entry with the same values.</returns>
    public JournalEntry ToEntry() => new(Id, OccurredUtc, EventType, AggregateId, PayloadJson);
}
