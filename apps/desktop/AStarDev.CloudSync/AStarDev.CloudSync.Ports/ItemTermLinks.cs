using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>The terms that now apply to one item, as worked out by classification.</summary>
/// <param name="ItemId">The classified item.</param>
/// <param name="TermIds">Every term that applies. Empty means the item is unclassified. Duplicates are ignored.</param>
public sealed record ItemTermLinks(ItemId ItemId, IReadOnlyList<TermId> TermIds);
