namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to scan the file paths for words that are not yet terms and return one page of them. The scan reads every path on each call, so it can take a while on a large index.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the scan cannot run.
/// </summary>
/// <param name="Skip">How many of the most common words to pass over. Zero or more; a negative value counts as zero.</param>
/// <param name="Take">The most words to return. The daemon returns between 1 and 500.</param>
/// <example>
/// <code>
/// var next = await client.QueryAsync(new GetClassificationSuggestions(Skip: shown), cancellationToken);
/// </code>
/// </example>
public sealed record GetClassificationSuggestions(int Skip = 0, int Take = 200) : IQuery<ClassificationSuggestionsView>;
