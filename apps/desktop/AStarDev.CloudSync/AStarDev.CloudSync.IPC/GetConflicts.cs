namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon which conflicts of a job wait for the user, deferred ones included, each with its path and what both sides hold. Ask once after connecting, then follow <see cref="ConflictDetected"/> and <see cref="ConflictResolved"/>.
/// Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the conflicts cannot be read.
/// </summary>
/// <param name="SyncJobId">The <see cref="SyncJobView.Id"/> of the job to list. An unknown job lists no conflicts.</param>
/// <param name="Skip">How many of the oldest conflicts to pass over. Zero or more; a negative value counts as zero.</param>
/// <param name="Take">The most conflicts to return. The daemon returns between 1 and 500.</param>
/// <example>
/// <code>
/// var conflicts = await client.QueryAsync(new GetConflicts(job.Id), cancellationToken);
/// </code>
/// </example>
public sealed record GetConflicts(Guid SyncJobId, int Skip = 0, int Take = 200) : IQuery<ConflictsView>;
