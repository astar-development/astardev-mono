namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for the audit trail in a time range. Entries older than the audit retention setting are pruned first, so they are never listed.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the trail cannot be read.
/// </summary>
/// <param name="FromUtc">The start of the range, inclusive.</param>
/// <param name="ToUtc">The end of the range, inclusive.</param>
/// <param name="EventTypes">The kinds of entry to list, for example <c>ConflictResolved</c>; empty lists every kind.</param>
/// <example>
/// <code>
/// var audit = await client.QueryAsync(new GetAudit(from, to, []), cancellationToken);
/// </code>
/// </example>
public sealed record GetAudit(DateTimeOffset FromUtc, DateTimeOffset ToUtc, IReadOnlyList<string> EventTypes) : IQuery<AuditView>;
