using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>How far the initial crawl of one scope has got. A scope never crawled has <see cref="None"/>.</summary>
/// <param name="ResumeToken">Where the provider continues the listing; <see langword="null"/> when the crawl has not started or has finished.</param>
/// <param name="ItemsIndexed">How many items the crawl has stored so far.</param>
/// <param name="Completed"><see langword="true"/> once the whole scope has been listed and stored; the crawl is never repeated for a completed scope.</param>
/// <param name="DeltaToken">The provider's bookmark taken when the listing completed; <see langword="null"/> until then, and when the provider gave none.</param>
public sealed record CrawlCheckpoint(string? ResumeToken, long ItemsIndexed, bool Completed, DeltaToken? DeltaToken)
{
    /// <summary>The checkpoint of a scope that has not been crawled at all.</summary>
    public static CrawlCheckpoint None { get; } = new(null, 0, false, null);
}
