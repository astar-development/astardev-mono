namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to stop synchronising a remote folder of a job. Answered with <see cref="CommandOutcome.Accepted"/> once removed, or
/// <see cref="CommandOutcome.Rejected"/> with a message the UI can show as it is (the job does not hold the folder).
/// </summary>
/// <param name="SyncJobId">The <see cref="SyncJobView.Id"/> of the job that loses the folder.</param>
/// <param name="RemoteRootId">The <see cref="RemoteFolderView.Id"/> of the folder to stop synchronising.</param>
/// <example>
/// <code>
/// var result = await client.SendAsync(new RemoveSyncScope(job.Id, folder.Id), cancellationToken);
/// </code>
/// </example>
public sealed record RemoveSyncScope(Guid SyncJobId, string RemoteRootId) : ICommand;
