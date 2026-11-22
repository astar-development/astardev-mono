namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to stop crawling a job and answers once the crawl has ended. Always <see cref="CommandOutcome.Accepted"/>, also when nothing was running.
/// What was stored stays, and <see cref="StartSync"/> continues from it.
/// </summary>
/// <param name="SyncJobId">The <see cref="SyncJobView.Id"/> of the job to stop.</param>
public sealed record StopSync(Guid SyncJobId) : ICommand;
