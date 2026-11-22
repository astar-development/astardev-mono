namespace AStarDev.CloudSync.IPC;

/// <summary>
/// Asks the daemon to start crawling the folders selected for a job. Answered with <see cref="CommandOutcome.Accepted"/> once the crawl runs, also when it was already running,
/// or <see cref="CommandOutcome.Rejected"/> with a message the UI can show as it is when the job does not exist or the database fails. The answer does not wait for the crawl;
/// follow it with <see cref="CrawlProgressChanged"/>. A crawl stopped by a restart or <see cref="StopSync"/> continues from where it stopped.
/// </summary>
/// <param name="SyncJobId">The <see cref="SyncJobView.Id"/> of the job to crawl.</param>
/// <example>
/// <code>
/// var result = await client.SendAsync(new StartSync(job.Id), cancellationToken);
/// </code>
/// </example>
public sealed record StartSync(Guid SyncJobId) : ICommand;
