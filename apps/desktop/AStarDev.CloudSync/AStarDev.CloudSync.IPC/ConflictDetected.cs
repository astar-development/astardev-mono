namespace AStarDev.CloudSync.IPC;

/// <summary>Published once when the daemon stores a new open conflict, after it is persisted and journaled. Delivered only to clients that called <see cref="IIpcClient.SubscribeAsync"/> and registered a handler; a client that connects later learns of it through <see cref="GetConflicts"/>.</summary>
/// <param name="Conflict">The new conflict.</param>
public sealed record ConflictDetected(ConflictView Conflict) : IEvent;
