using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.Ports;

/// <summary>The last reconciled state of one item.</summary>
/// <param name="ItemId">The item the baseline belongs to.</param>
/// <param name="Snapshot">What both sides were last successfully brought to.</param>
public sealed record KnownEntry(ItemId ItemId, KnownSnapshot Snapshot);
