namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for the metric samples it has stored, newest first and at most 2000. Every filter narrows the result; the defaults match everything. Samples taken before a daemon restart are included.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the telemetry store cannot be read.
/// </summary>
/// <param name="Since">Samples taken before this instant are left out. The default value keeps everything.</param>
/// <param name="Name">Only samples of the instrument with exactly this name, such as <c>cloudsync.queue.pending</c>, are returned. Empty keeps every instrument.</param>
/// <example>
/// <code>
/// var metrics = await client.QueryAsync(new GetMetrics(Name: "cloudsync.queue.pending"), cancellationToken);
/// </code>
/// </example>
public sealed record GetMetrics(DateTimeOffset Since = default, string Name = "") : IQuery<MetricsView>;
