namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetLogs"/>.</summary>
/// <param name="Entries">The matching entries, newest first; empty when nothing matches. Messages carry ids and counts only, never paths or e-mail addresses.</param>
public sealed record LogsView(IReadOnlyList<LogEntryView> Entries);
