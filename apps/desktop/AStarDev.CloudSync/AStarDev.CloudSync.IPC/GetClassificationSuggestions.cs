namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to scan the file paths for words that are not yet terms. Needs no parameters. The scan reads every path, so it can take a while on a large index.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the scan cannot run.
/// </summary>
/// <example>
/// <code>
/// var suggestions = await client.QueryAsync(new GetClassificationSuggestions(), cancellationToken);
/// </code>
/// </example>
public sealed record GetClassificationSuggestions : IQuery<ClassificationSuggestionsView>;
