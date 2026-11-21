namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon which remote folders a job synchronises. Fails with <see cref="HandlerFailed"/>, carrying a message fit for display, when the scopes cannot be read.
/// </summary>
/// <param name="SyncJobId">The <see cref="SyncJobView.Id"/> of the job to list. An unknown job lists no scopes.</param>
/// <example>
/// <code>
/// var scopes = await client.QueryAsync(new GetSyncScopes(job.Id), cancellationToken);
/// </code>
/// </example>
public sealed record GetSyncScopes(Guid SyncJobId) : IQuery<SyncScopesView>;
