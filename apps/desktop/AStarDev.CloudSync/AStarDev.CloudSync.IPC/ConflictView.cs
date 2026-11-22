namespace AStarDev.CloudSync.IPC;

/// <summary>One conflict waiting for the user. It carries identities, not paths: the daemon does not resolve the item's path for the conflict.</summary>
/// <param name="ConflictId">Identifies the conflict, for the later resolve command.</param>
/// <param name="SyncJobId">The job the conflict belongs to.</param>
/// <param name="ItemId">The daemon's identity for the file in dispute.</param>
/// <param name="Type">The competing intents as the name of the conflict type, for example <c>ModifyModify</c>.</param>
/// <param name="DetectedUtc">When the daemon detected the conflict.</param>
public sealed record ConflictView(Guid ConflictId, Guid SyncJobId, Guid ItemId, string Type, DateTimeOffset DetectedUtc);
