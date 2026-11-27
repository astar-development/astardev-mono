namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for the audit trail in a time range. Entries older than the audit retention setting are pruned first, so they are never listed.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the trail cannot be read.
/// </summary>
/// <param name="FromUtc">The start of the range, inclusive.</param>
/// <param name="ToUtc">The end of the range, inclusive.</param>
/// <param name="EventTypes">The kinds of entry to list, for example <c>ConflictResolved</c>; empty lists every kind.</param>
/// <param name="Skip">How many of the newest matching entries to pass over. Zero or more; a negative value counts as zero.</param>
/// <param name="Take">The most entries to return. The daemon returns between 1 and 500.</param>
/// <example>
/// <code>
/// var audit = await client.QueryAsync(new GetAudit(from, to, []), cancellationToken);
/// </code>
/// </example>
public sealed record GetAudit(DateTimeOffset FromUtc, DateTimeOffset ToUtc, IReadOnlyList<string> EventTypes, int Skip = 0, int Take = 200) : IQuery<AuditView>;
