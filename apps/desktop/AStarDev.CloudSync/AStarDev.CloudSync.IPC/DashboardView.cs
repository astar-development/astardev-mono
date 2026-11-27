namespace AStarDev.CloudSync.IPC;

/// <summary>What the dashboard shows, answering <see cref="GetDashboardSnapshot"/> and carried by <see cref="DashboardChanged"/>. The daemon counts and estimates; the UI only shows the numbers.</summary>
/// <param name="ProviderHealth">The provider health check's verdict.</param>
/// <param name="Stage">The pipeline stage the daemon is in; <see langword="null"/> when no sync has run yet, so no stage is current and none is done.</param>
/// <param name="LastReconciliation">When a job last finished indexing or applying changes; <see langword="null"/> before any has since the daemon started.</param>
/// <param name="LastProviderPoll">When the provider last answered with progress since the daemon started; <see langword="null"/> before any.</param>
/// <param name="LastQueueActivity">When the newest operation in the queue was created; <see langword="null"/> when the queue is empty.</param>
/// <param name="ItemsAnalysed">Items processed so far: indexed while any job is indexing, otherwise operations completed.</param>
/// <param name="ItemsTotal">Items to process in all: the indexing total (<see langword="null"/> when any job's is unknown) while indexing, otherwise every operation queued so far.</param>
/// <param name="ItemsPerSecond">Items per second: the crawl's, summed over the folders being crawled, while indexing, otherwise operations completed per second over the last minute; <see langword="null"/> when no rate is known yet.</param>
/// <param name="EstimatedRemaining">Time left: the longest crawl estimate while indexing, otherwise the operations not yet completed at the current rate; <see langword="null"/> when unknown.</param>
/// <param name="Providers">One entry per provider type with an account.</param>
/// <param name="Pending">Operations waiting to start.</param>
/// <param name="Running">Operations executing now.</param>
/// <param name="Completed">Operations finished successfully.</param>
/// <param name="Failed">Operations whose last attempt failed and wait for a retry.</param>
/// <param name="Conflicts">Conflicts waiting for the user, deferred ones included.</param>
/// <param name="Errors">Operations that gave up plus sync jobs that stopped on an error, all waiting for a decision.</param>
/// <param name="Warnings">Providers that need the user, for example to sign in again.</param>
/// <param name="Activities">The operations executing now.</param>
/// <param name="Events">Recent events as names, newest first: <c>ReconciliationCompleted</c>, <c>QueueDrained</c> or <c>SyncScopeUpdated</c>.</param>
/// <param name="Setup">Whether an account is connected and folders are selected; anything but <see cref="DashboardSetup.Ready"/> means there is nothing to sync yet.</param>
public sealed record DashboardView(
    ProviderHealthState ProviderHealth,
    DashboardStage? Stage,
    DateTimeOffset? LastReconciliation,
    DateTimeOffset? LastProviderPoll,
    DateTimeOffset? LastQueueActivity,
    long ItemsAnalysed,
    long? ItemsTotal,
    double? ItemsPerSecond,
    TimeSpan? EstimatedRemaining,
    IReadOnlyList<DashboardProviderView> Providers,
    int Pending,
    int Running,
    int Completed,
    int Failed,
    int Conflicts,
    int Errors,
    int Warnings,
    IReadOnlyList<DashboardOperationView> Activities,
    IReadOnlyList<string> Events,
    DashboardSetup Setup);
