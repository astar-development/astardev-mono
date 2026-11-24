using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Links one item to one term. Keyed by both, so an item holds each term once; deleting either side deletes the link.</summary>
internal sealed class FileLinkEntity
{
    /// <summary>The linked item.</summary>
    public ItemId ItemId { get; set; }

    /// <summary>The linked term.</summary>
    public TermId TermId { get; set; }

    /// <summary>Whether classification or the user made the link.</summary>
    public FileLinkOrigin Origin { get; set; }
}
