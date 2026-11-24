namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for one page of the files linked to a term. A term can have hundreds of thousands of files, so the caller pages.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the files cannot be read.
/// </summary>
/// <param name="TermId">The term, as in <see cref="ClassificationTermView.Id"/>.</param>
/// <param name="Skip">How many files to skip, in path order. Zero or more.</param>
/// <param name="Take">The most files to return. At least one; the daemon returns at most 500.</param>
/// <example>
/// <code>
/// var page = await client.QueryAsync(new GetTermFiles(termId, 0, 100), cancellationToken);
/// </code>
/// </example>
public sealed record GetTermFiles(Guid TermId, int Skip, int Take) : IQuery<TermFilesView>;
