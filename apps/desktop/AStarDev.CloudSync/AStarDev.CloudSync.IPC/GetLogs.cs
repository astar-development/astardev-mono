namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for the log entries it has stored, newest first and at most 500. Every filter narrows the result; the defaults match everything. Logs written before a daemon restart are included.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the telemetry store cannot be read.
/// </summary>
/// <param name="MinimumLevel">Entries below this <c>LogLevel</c> number are left out. Zero keeps every level.</param>
/// <param name="Since">Entries stamped before this instant are left out. The default value keeps everything.</param>
/// <param name="Text">Entries whose message does not contain this text, ignoring ASCII case, are left out. Empty keeps every entry.</param>
/// <example>
/// <code>
/// var logs = await client.QueryAsync(new GetLogs(MinimumLevel: 3), cancellationToken);
/// </code>
/// </example>
public sealed record GetLogs(int MinimumLevel = 0, DateTimeOffset Since = default, string Text = "") : IQuery<LogsView>;
