namespace AStarDev.CloudSync.IPC;

/// <summary>One recorded significant action.</summary>
/// <param name="Id">Identity of the entry.</param>
/// <param name="OccurredUtc">When the action happened.</param>
/// <param name="EventType">The kind of action, for example <c>SyncScopeAdded</c> or <c>ConflictResolved</c>.</param>
/// <param name="AggregateId">The identity of what the action concerns, for example a conflict, a sync job or an account.</param>
/// <param name="DetailsJson">What the action did, as JSON text; <c>{}</c> when there is nothing to add.</param>
public sealed record AuditEntryView(Guid Id, DateTimeOffset OccurredUtc, string EventType, Guid AggregateId, string DetailsJson);
