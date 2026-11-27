namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to create the sync job for an account. Connecting an account creates no job. Each account has one job, so a second request for the same account is declined.
/// Answered with <see cref="CommandOutcome.Accepted"/> once the job is stored, or <see cref="CommandOutcome.Rejected"/> with a message the UI can show as it is
/// (a job already exists, the local folder does not exist, the account is unknown). Nothing is synchronised until scopes are added with <see cref="AddSyncScope"/>.
/// </summary>
/// <param name="AccountId">The <see cref="AccountView.Id"/> of the account to synchronise.</param>
/// <param name="LocalRootPath">Absolute path of an existing local folder. A path that does not exist, or is a file, is rejected.</param>
/// <param name="BootstrapMode">How the first synchronisation treats content present on both sides. Stored now, acted on by the initial crawl.</param>
/// <example>
/// <code>
/// var result = await client.SendAsync(new CreateSyncJob(account.Id, "/home/ada/OneDrive", SyncBootstrapMode.Merge), cancellationToken);
/// </code>
/// </example>
public sealed record CreateSyncJob(Guid AccountId, string LocalRootPath, SyncBootstrapMode BootstrapMode) : ICommand;
