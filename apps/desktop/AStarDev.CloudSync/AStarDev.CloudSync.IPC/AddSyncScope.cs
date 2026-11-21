namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to synchronise one more remote folder as part of a job. Answered with <see cref="CommandOutcome.Accepted"/> once stored, or
/// <see cref="CommandOutcome.Rejected"/> with a message the UI can show as it is (unknown job, folder already selected).
/// </summary>
/// <param name="SyncJobId">The <see cref="SyncJobView.Id"/> of the job that gains the folder.</param>
/// <param name="RemoteRootId">The <see cref="RemoteFolderView.Id"/> of the folder to synchronise.</param>
/// <example>
/// <code>
/// var result = await client.SendAsync(new AddSyncScope(job.Id, folder.Id), cancellationToken);
/// </code>
/// </example>
public sealed record AddSyncScope(Guid SyncJobId, string RemoteRootId) : ICommand;
