namespace AStarDev.CloudSync.Ports;

/// <summary>One significant occurrence kept for diagnostics, supportability and audit.</summary>
/// <param name="Id">Our identity for the entry.</param>
/// <param name="OccurredUtc">When the occurrence happened, not when the entry was stored.</param>
/// <param name="EventType">The kind of occurrence, for example <c>ConflictDetected</c>.</param>
/// <param name="AggregateId">The identity of the thing the occurrence is about, for example a conflict id; entries of one aggregate list together.</param>
/// <param name="PayloadJson">Diagnostic details as JSON text; never empty, <c>{}</c> when there is nothing to add.</param>
public sealed record JournalEntry(Guid Id, DateTimeOffset OccurredUtc, string EventType, Guid AggregateId, string PayloadJson);
