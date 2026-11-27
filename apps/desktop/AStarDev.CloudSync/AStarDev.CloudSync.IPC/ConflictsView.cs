namespace AStarDev.CloudSync.IPC;

/// <summary>Answer to <see cref="GetConflicts"/>.</summary>
/// <param name="Total">How many open and deferred conflicts the job has in all, so a caller can ask for the next page. Zero for an unknown job.</param>
/// <param name="Conflicts">This page of the job's open and deferred conflicts, oldest first; empty when none, when the job is unknown or when the page is past the end.</param>
public sealed record ConflictsView(int Total, IReadOnlyList<ConflictView> Conflicts);
