using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>The terms that now apply to one item, named by the provider's identity because the caller has the report of a crawl or delta page and not our own item identity.</summary>
/// <param name="RemoteItemId">The provider's identity for the classified item.</param>
/// <param name="TermIds">Every term that applies. Empty means the item is unclassified. Duplicates are ignored.</param>
public sealed record RemoteItemTermLinks(RemoteItemId RemoteItemId, IReadOnlyList<TermId> TermIds);
