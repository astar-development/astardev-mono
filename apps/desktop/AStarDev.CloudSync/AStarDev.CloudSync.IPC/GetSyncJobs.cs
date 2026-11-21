namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon for every sync job. Needs no parameters. Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the jobs cannot be read.
/// </summary>
/// <example>
/// <code>
/// var jobs = await client.QueryAsync(new GetSyncJobs(), cancellationToken);
/// </code>
/// </example>
public sealed record GetSyncJobs : IQuery<SyncJobsView>;
