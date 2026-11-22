namespace AStarDev.CloudSync.Domain;

/// <summary>Our own identity for one queued unit of work.</summary>
/// <param name="Value">Unique per operation.</param>
public readonly record struct OperationId(Guid Value);
