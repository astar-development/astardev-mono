namespace AStarDev.CloudSync.IPC;

/// <summary>The audit trail answering <see cref="GetAudit"/>.</summary>
/// <param name="Entries">Oldest first. Empty when nothing was recorded in the range.</param>
public sealed record AuditView(IReadOnlyList<AuditEntryView> Entries);
