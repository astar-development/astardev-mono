using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AStarDev.CloudSync.Daemon;

/// <summary>
/// Answers <see cref="GetDashboardSnapshot"/> from the operation, conflict, account and journal stores, the provider health check and the <see cref="ActivityTracker"/>, and publishes <see cref="DashboardChanged"/> when the answer changes.
/// Items done and total come from the <see cref="ActivityTracker"/>; throughput and time left come only from the crawl's own estimate (<see cref="CrawlProgress.ItemsPerSecond"/>), not from the tracker's per-job estimate.
/// </summary>
internal sealed class DashboardSnapshotQuery(IOperationStore operations, IConflictStore conflicts, IAccountStore accounts, IEventJournal journal, CanonicalStateReader states, HealthCheckService health, ActivityTracker tracker, IIpcServer server, TimeProvider timeProvider)
{
    private static readonly string[] ScopeEvents = ["SyncScopeAdded", "SyncScopeRemoved"];

    private readonly object gate = new();
    private readonly Dictionary<(Guid Job, string Root), (double? Rate, TimeSpan? Remaining)> crawls = [];
    private Option<DashboardView> published = Option.None<DashboardView>();

    /// <summary>Remembers the crawl's own throughput and estimate for the folder, until the folder completes or fails.</summary>
    /// <param name="progress">The report. One without a rate clears the folder's earlier rate.</param>
    public void Observe(CrawlProgress progress)
    {
        lock (gate)
        {
            var key = (progress.SyncJobId.Value, progress.RemoteRootId.Value);
            if (progress.Completed || progress.Failed) crawls.Remove(key);
            else crawls[key] = (progress.ItemsPerSecond, progress.EstimatedRemaining);
        }
    }

    /// <summary>Reads the dashboard values now.</summary>
    /// <returns>The snapshot, or <see cref="HandlerFailed"/> carrying the first store failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<DashboardView, IpcError>> HandleAsync(GetDashboardSnapshot query, CancellationToken cancellationToken)
    {
        var report = await health.CheckHealthAsync(registration => registration.Name == ApplicationConstants.ProviderHealthCheckName, cancellationToken);
        var counts = await operations.CountByStatusAsync(cancellationToken);
        if (counts.Value is Fail<StoreError> countsFailure) return Failure(countsFailure.Error, "operation queue");

        var open = await conflicts.CountOpenAsync(cancellationToken);
        if (open.Value is Fail<StoreError> openFailure) return Failure(openFailure.Error, "conflicts");

        var accountList = await accounts.ListAsync(cancellationToken);
        if (accountList.Value is Fail<StoreError> accountsFailure) return Failure(accountsFailure.Error, "accounts");

        var all = await operations.ListAsync(cancellationToken);
        if (all.Value is Fail<StoreError> allFailure) return Failure(allFailure.Error, "operation queue");

        var scopeEntries = await journal.ListAsync(DateTimeOffset.UnixEpoch, timeProvider.GetUtcNow().AddDays(1), ScopeEvents, cancellationToken);
        if (scopeEntries.Value is Fail<StoreError> journalFailure) return Failure(journalFailure.Error, "recent events");

        var queue = ((Ok<IReadOnlyList<Operation>>)all.Value).Value;
        var running = await RunningAsync(queue.Where(operation => operation.Status == OperationStatus.Running).ToList(), cancellationToken);
        if (running.Value is Fail<StoreError> runningFailure) return Failure(runningFailure.Error, "running operations");

        var view = ViewOf(report.Status, ((Ok<IReadOnlyDictionary<OperationStatus, int>>)counts.Value).Value, ((Ok<int>)open.Value).Value, ((Ok<IReadOnlyList<Account>>)accountList.Value).Value, queue, ((Ok<IReadOnlyList<DashboardOperationView>>)running.Value).Value, ((Ok<IReadOnlyList<JournalEntry>>)scopeEntries.Value).Value);

        return new Ok<DashboardView>(view);
    }

    /// <summary>Publishes <see cref="DashboardChanged"/> when the dashboard differs from the one published last. The first call always publishes.</summary>
    /// <returns>Success when nothing needed publishing or the event went out; otherwise the read or publish failure. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<Unit, IpcError>> PublishIfChangedAsync(CancellationToken cancellationToken)
        => await HandleAsync(new GetDashboardSnapshot(), cancellationToken) switch
        {
            Ok<DashboardView> dashboard => HasChanged(dashboard.Value) ? await server.PublishAsync(new DashboardChanged(dashboard.Value), cancellationToken) : new Ok<Unit>(default),
            Fail<IpcError> fail => fail
        };

    private async Task<Result<IReadOnlyList<DashboardOperationView>, StoreError>> RunningAsync(IReadOnlyList<Operation> running, CancellationToken cancellationToken)
    {
        var paths = new Dictionary<ItemId, string>();
        foreach (var jobId in running.Select(operation => operation.SyncJobId).Distinct())
        {
            var read = await states.ReadAsync(jobId, cancellationToken);
            if (read.Value is Fail<StoreError> failure) return failure;

            foreach (var state in ((Ok<IReadOnlyList<CanonicalItemState>>)read.Value).Value) paths[state.ItemId] = state.Remote?.Path.Value ?? state.Local?.Path.Value ?? state.Known?.Path.Value ?? string.Empty;
        }

        return new Ok<IReadOnlyList<DashboardOperationView>>(running.Select(operation => new DashboardOperationView(operation.Type.ToString(), paths.GetValueOrDefault(operation.ItemId, string.Empty))).ToList());
    }

    private bool HasChanged(DashboardView dashboard)
    {
        lock (gate)
        {
            if (published.Value is Some<DashboardView> last && Same(last.Value, dashboard)) return false;

            published = Option.Some(dashboard);

            return true;
        }
    }

    private static bool Same(DashboardView left, DashboardView right)
        => left with { Providers = [], Activities = [], Events = [] } == right with { Providers = [], Activities = [], Events = [] }
            && left.Providers.SequenceEqual(right.Providers)
            && left.Activities.SequenceEqual(right.Activities)
            && left.Events.SequenceEqual(right.Events);

    private DashboardView ViewOf(HealthStatus providerHealth, IReadOnlyDictionary<OperationStatus, int> counts, int openConflicts, IReadOnlyList<Account> accountList, IReadOnlyList<Operation> queue, IReadOnlyList<DashboardOperationView> running, IReadOnlyList<JournalEntry> scopeEntries)
    {
        var activities = tracker.Current().Activities;
        var pending = counts.GetValueOrDefault(OperationStatus.Pending);
        var executing = counts.GetValueOrDefault(OperationStatus.Running);
        var completed = counts.GetValueOrDefault(OperationStatus.Completed);
        var providers = ProvidersOf(accountList);
        var reconciled = activities.Where(activity => activity.State == ActivityState.Healthy).Select(activity => (DateTimeOffset?)activity.UpdatedUtc).Max();
        var queued = queue.Select(operation => (DateTimeOffset?)operation.CreatedUtc).Max();
        var (rate, remaining) = CrawlEstimate();

        return new DashboardView(
            ProviderHealthOf(providerHealth),
            StageOf(activities, pending + executing),
            reconciled,
            activities.Select(activity => (DateTimeOffset?)activity.UpdatedUtc).Max(),
            queued,
            activities.Sum(activity => activity.ItemsDone),
            activities.Count > 0 && activities.All(activity => activity.ItemsTotal is not null) ? activities.Sum(activity => activity.ItemsTotal!.Value) : null,
            rate,
            remaining,
            providers,
            pending,
            executing,
            completed,
            counts.GetValueOrDefault(OperationStatus.Failed),
            openConflicts,
            counts.GetValueOrDefault(OperationStatus.DeadLetter),
            providers.Count(provider => provider.Health != ProviderHealthState.Healthy),
            running,
            EventsOf(reconciled, pending + executing == 0 && completed > 0 ? queued ?? DateTimeOffset.UnixEpoch : null, scopeEntries.Select(entry => (DateTimeOffset?)entry.OccurredUtc).Max()));
    }

    private (double? Rate, TimeSpan? Remaining) CrawlEstimate()
    {
        lock (gate)
        {
            var rates = crawls.Values.Where(crawl => crawl.Rate is not null).Select(crawl => crawl.Rate!.Value).ToList();
            var remaining = crawls.Values.Where(crawl => crawl.Remaining is not null).Select(crawl => (TimeSpan?)crawl.Remaining!.Value).Max();

            return (rates.Count == 0 ? null : rates.Sum(), remaining);
        }
    }

    private static IReadOnlyList<string> EventsOf(DateTimeOffset? reconciled, DateTimeOffset? drained, DateTimeOffset? scopeUpdated)
        => new[] { ("ReconciliationCompleted", reconciled), ("QueueDrained", drained), ("SyncScopeUpdated", scopeUpdated) }
            .Where(item => item.Item2 is not null)
            .OrderByDescending(item => item.Item2)
            .Select(item => item.Item1)
            .ToList();

    private static IReadOnlyList<DashboardProviderView> ProvidersOf(IReadOnlyList<Account> accountList)
        => accountList.GroupBy(account => account.ProviderType).Select(group => new DashboardProviderView(group.Key, group.Any(account => account.Status == AccountStatus.ReauthenticationRequired) ? ProviderHealthState.Degraded : ProviderHealthState.Healthy)).ToList();

    private static DashboardStage StageOf(IReadOnlyList<ActivityView> activities, int queued)
    {
        if (activities.Any(activity => activity.State == ActivityState.InitialIndexing)) return DashboardStage.EnumerateRemoteState;
        if (queued > 0) return DashboardStage.ExecuteQueue;

        return activities.Any(activity => activity.State == ActivityState.Working) ? DashboardStage.AnalyseChanges : DashboardStage.ExecuteQueue;
    }

    private static ProviderHealthState ProviderHealthOf(HealthStatus health) => health switch
    {
        HealthStatus.Healthy => ProviderHealthState.Healthy,
        HealthStatus.Degraded => ProviderHealthState.Degraded,
        _ => ProviderHealthState.Unhealthy
    };

    private static Fail<IpcError> Failure(StoreError error, string subject) => new(new HandlerFailed(StoreErrorMessages.Describe(error, "read", subject)));
}
