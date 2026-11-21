namespace AStarDev.CloudSync.IPC;

/// <summary>The sync jobs the daemon holds, answering <see cref="GetSyncJobs"/>.</summary>
/// <param name="Jobs">Oldest first. Empty until a job is created, which connecting an account does not do.</param>
public sealed record SyncJobsView(IReadOnlyList<SyncJobView> Jobs);
