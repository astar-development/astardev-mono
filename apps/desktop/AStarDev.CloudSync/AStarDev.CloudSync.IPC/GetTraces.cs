namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for the spans it has stored, newest first and at most 500. Every filter narrows the result; the defaults match everything. Spans recorded before a daemon restart are included.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the telemetry store cannot be read.
/// </summary>
/// <param name="Since">Spans that started before this instant are left out. The default value keeps everything.</param>
/// <param name="TraceId">Only spans of this trace, as 32 lowercase hexadecimal characters, are returned. Empty keeps every trace.</param>
/// <example>
/// <code>
/// var traces = await client.QueryAsync(new GetTraces(TraceId: traceId), cancellationToken);
/// </code>
/// </example>
public sealed record GetTraces(DateTimeOffset Since = default, string TraceId = "") : IQuery<TracesView>;
