namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for the audit trail as CSV, with the same entries <see cref="GetAudit"/> lists for the same <paramref name="Filter"/>, including its retention pruning.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the trail cannot be read.
/// </summary>
/// <param name="Filter">The range and entry kinds the screen shows.</param>
/// <example>
/// <code>
/// var export = await client.QueryAsync(new ExportAudit(new GetAudit(from, to, [])), cancellationToken);
/// </code>
/// </example>
public sealed record ExportAudit(GetAudit Filter) : IQuery<ExportView>;
