namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for every classification category and term, each term with the number of files linked to it. Needs no parameters.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the vocabulary cannot be read.
/// </summary>
/// <example>
/// <code>
/// var vocabulary = await client.QueryAsync(new GetClassificationTerms(), cancellationToken);
/// </code>
/// </example>
public sealed record GetClassificationTerms : IQuery<ClassificationTermsView>;
