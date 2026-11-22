namespace AStarDev.CloudSync.IPC;

/// <summary>Published when a conflict leaves the open list because it was resolved; the conflict no longer appears in <see cref="GetConflicts"/>. Delivered only to subscribed clients.</summary>
/// <param name="ConflictId">The <see cref="ConflictView.ConflictId"/> that was resolved.</param>
/// <param name="SyncJobId">The job the conflict belonged to.</param>
/// <param name="ItemId">The file that was in dispute.</param>
/// <param name="Resolution">What was chosen, as the name of the resolution, for example <c>KeepLocal</c>.</param>
/// <param name="ResolvedUtc">When the resolution was applied.</param>
public sealed record ConflictResolved(Guid ConflictId, Guid SyncJobId, Guid ItemId, string Resolution, DateTimeOffset ResolvedUtc) : IEvent;
