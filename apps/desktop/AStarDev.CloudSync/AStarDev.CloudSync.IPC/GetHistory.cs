namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon which operations have finished, completed or failed, newest first. Every filter is optional and a filter left <see langword="null"/> does not narrow the list; filters combine with AND.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the operations cannot be read.
/// </summary>
/// <param name="Provider">Provider type to keep, for example <c>OneDrive</c>, compared ignoring case.</param>
/// <param name="Type">Operation type to keep by its name, for example <c>UploadFile</c>, compared ignoring case.</param>
/// <param name="Status">Outcome to keep: <c>Completed</c> or <c>Failed</c>, compared ignoring case. Any other text matches nothing.</param>
/// <param name="From">Keep operations recorded at or after this instant.</param>
/// <param name="To">Keep operations recorded at or before this instant.</param>
/// <param name="Path">Text the affected path must contain, compared ignoring case. A blank text does not narrow the list.</param>
/// <example>
/// <code>
/// var history = await client.QueryAsync(new GetHistory(Status: "Failed"), cancellationToken);
/// </code>
/// </example>
public sealed record GetHistory(string? Provider = null, string? Type = null, string? Status = null, DateTimeOffset? From = null, DateTimeOffset? To = null, string? Path = null) : IQuery<HistoryView>;
