namespace AStarDev.CloudSync.IPC;

/// <summary>One page of the audit trail, answering <see cref="GetAudit"/>.</summary>
/// <param name="Total">How many entries match the query in all, so a caller can ask for the next page. At least <c>Entries.Count</c>.</param>
/// <param name="Entries">The entries of this page, newest first. Empty when nothing was recorded in the range or the page is past the end.</param>
public sealed record AuditView(int Total, IReadOnlyList<AuditEntryView> Entries);
