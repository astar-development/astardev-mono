namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to synchronise a local top-level folder that no scope covers: the daemon creates the remote folder of the same name at the top of the job's drive,
/// reusing one already there, then adds it as a scope exactly as <see cref="AddSyncScope"/> does. Answered with <see cref="CommandOutcome.Accepted"/> once stored, or
/// <see cref="CommandOutcome.ValidationError"/> when the name is not one folder, or <see cref="CommandOutcome.Rejected"/>; both carry a message the UI can show as it is (unknown job, remote folder that could not be created, folder already selected).
/// </summary>
/// <param name="SyncJobId">The <see cref="SyncJobView.Id"/> of the job that gains the folder; the job decides the account whose drive gets the folder.</param>
/// <param name="FolderName">One name from <see cref="SyncScopesView.UnsyncedFolders"/>: a single folder directly under the job's local root, never a path.</param>
/// <example>
/// <code>
/// var result = await client.SendAsync(new AddLocalFolderScope(job.Id, "Photos"), cancellationToken);
/// </code>
/// </example>
public sealed record AddLocalFolderScope(Guid SyncJobId, string FolderName) : ICommand;
