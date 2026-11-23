namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for the history as CSV, with the same rows <see cref="GetHistory"/> lists for the same <paramref name="Filter"/>.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the operations cannot be read.
/// </summary>
/// <param name="Filter">The filters the screen shows; a filter left <see langword="null"/> does not narrow the export.</param>
/// <example>
/// <code>
/// var export = await client.QueryAsync(new ExportHistory(new GetHistory(Status: "Failed")), cancellationToken);
/// </code>
/// </example>
public sealed record ExportHistory(GetHistory Filter) : IQuery<ExportView>;
