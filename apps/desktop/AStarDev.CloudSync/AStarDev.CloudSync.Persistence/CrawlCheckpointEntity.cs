using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.Persistence;

/// <summary>How far the initial crawl of one scope has got. At most one row per scope; it goes when the scope goes.</summary>
internal sealed class CrawlCheckpointEntity
{
    /// <summary>The job of the crawled scope; part of the primary key.</summary>
    public SyncJobId SyncJobId { get; set; }

    /// <summary>The crawled folder; part of the primary key.</summary>
    public RemoteItemId RemoteRootId { get; set; }

    /// <summary>Where the provider continues the listing; <see langword="null"/> when the listing has finished.</summary>
    public string? ResumeToken { get; set; }

    /// <summary>How many items the crawl has stored for the scope.</summary>
    public long ItemsIndexed { get; set; }

    /// <summary>Whether the whole scope has been listed and stored.</summary>
    public bool Completed { get; set; }

    /// <summary>When the checkpoint was last written.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>Builds the port's view of the stored row.</summary>
    /// <param name="deltaToken">The scope's stored delta token, <see langword="null"/> when it has none.</param>
    /// <returns>An immutable checkpoint with the same values.</returns>
    public CrawlCheckpoint ToCheckpoint(DeltaToken? deltaToken) => new(ResumeToken, ItemsIndexed, Completed, deltaToken);
}
