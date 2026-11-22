namespace AStarDev.CloudSync.SyncEngine;

/// <summary>What one planning pass did.</summary>
/// <param name="Queued">How many operations were added to the queue.</param>
/// <param name="Conflicts">How many new conflicts were stored; a conflict that was already open is not counted.</param>
public sealed record PlanSummary(int Queued, int Conflicts);
