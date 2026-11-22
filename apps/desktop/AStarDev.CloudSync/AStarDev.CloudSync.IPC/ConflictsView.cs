namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetConflicts"/>.</summary>
/// <param name="Conflicts">The job's open and deferred conflicts, oldest first; empty when none or when the job is unknown.</param>
public sealed record ConflictsView(IReadOnlyList<ConflictView> Conflicts);
