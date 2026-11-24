namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for the classification categories and one page of the terms, each term with the number of files linked to it. The daemon filters, orders and pages, because the whole vocabulary is far larger than one message may be.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the vocabulary cannot be read.
/// </summary>
/// <param name="Filter">Only terms whose text or category name contains this, ignoring case, are returned. Empty returns every term.</param>
/// <param name="Sort">What the terms are ordered by.</param>
/// <param name="Descending"><see langword="true"/> to reverse the order.</param>
/// <param name="Skip">How many matching terms to skip, in the chosen order. Zero or more.</param>
/// <param name="Take">The most terms to return. At least one; the daemon returns at most 500.</param>
/// <example>
/// <code>
/// var page = await client.QueryAsync(new GetClassificationTerms("red", ClassificationTermSort.Files, true, 0, 200), cancellationToken);
/// </code>
/// </example>
public sealed record GetClassificationTerms(string Filter = "", ClassificationTermSort Sort = ClassificationTermSort.Text, bool Descending = false, int Skip = 0, int Take = 200) : IQuery<ClassificationTermsView>;
