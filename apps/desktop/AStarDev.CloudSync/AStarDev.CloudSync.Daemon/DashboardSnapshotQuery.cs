using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AStarDev.CloudSync.Daemon;

/// <summary>
/// Answers <see cref="GetDashboardSnapshot"/> from the operation, conflict, account and journal stores, the provider health check and the <see cref="ActivityTracker"/>, and publishes <see cref="DashboardChanged"/> when the answer changes.
/// While a job is indexing, items done and total, throughput and time left are the <see cref="ActivityTracker"/>'s (<see cref="ActivityView.ItemsPerSecond"/>, <see cref="ActivityView.EtaSeconds"/>), the same the Activity screen shows.
/// Afterwards they describe transfers: operations completed out of all operations, the <see cref="TransferRate"/> and the time left at that rate.
/// </summary>
internal sealed class DashboardSnapshotQuery(IOperationStore operations, IConflictStore conflicts, IAccountStore accounts, ISyncJobStore jobs, ISyncScopeStore scopes, IEventJournal journal, CanonicalStateReader states, HealthCheckService health, ActivityTracker tracker, TransferRate transferRate, IIpcServer server, TimeProvider timeProvider)
{
    private static readonly string[] ScopeEvents = ["SyncScopeAdded", "SyncScopeRemoved"];

    private readonly object gate = new();
    private Option<DashboardView> published = Option.None<DashboardView>();

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

        var setup = await SetupAsync(((Ok<IReadOnlyList<Account>>)accountList.Value).Value, cancellationToken);
        if (setup.Value is Fail<StoreError> setupFailure) return Failure(setupFailure.Error, "folder selection");

        var newest = await operations.ListNewestAsync(Enum.GetValues<OperationStatus>(), 1, cancellationToken);
        if (newest.Value is Fail<StoreError> newestFailure) return Failure(newestFailure.Error, "operation queue");

        var runningOperations = await operations.ListNewestAsync([OperationStatus.Running], int.MaxValue, cancellationToken);
        if (runningOperations.Value is Fail<StoreError> runningOperationsFailure) return Failure(runningOperationsFailure.Error, "operation queue");

        var scopeEntries = await journal.ListAsync(DateTimeOffset.UnixEpoch, timeProvider.GetUtcNow().AddDays(1), ScopeEvents, cancellationToken);
        if (scopeEntries.Value is Fail<StoreError> journalFailure) return Failure(journalFailure.Error, "recent events");

        var queue = ((Ok<IReadOnlyList<Operation>>)newest.Value).Value;
        var running = await RunningAsync(((Ok<IReadOnlyList<Operation>>)runningOperations.Value).Value, cancellationToken);
        if (running.Value is Fail<StoreError> runningFailure) return Failure(runningFailure.Error, "running operations");

        var view = ViewOf(report.Status, ((Ok<IReadOnlyDictionary<OperationStatus, int>>)counts.Value).Value, ((Ok<int>)open.Value).Value, ((Ok<IReadOnlyList<Account>>)accountList.Value).Value, queue, ((Ok<IReadOnlyList<DashboardOperationView>>)running.Value).Value, ((Ok<IReadOnlyList<JournalEntry>>)scopeEntries.Value).Value, ((Ok<DashboardSetup>)setup.Value).Value);

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
        foreach (var job in running.GroupBy(operation => operation.SyncJobId))
        {
            var read = await states.ReadAsync(job.Key, job.Select(operation => operation.ItemId).ToHashSet(), cancellationToken);
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

    private DashboardView ViewOf(HealthStatus providerHealth, IReadOnlyDictionary<OperationStatus, int> counts, int openConflicts, IReadOnlyList<Account> accountList, IReadOnlyList<Operation> queue, IReadOnlyList<DashboardOperationView> running, IReadOnlyList<JournalEntry> scopeEntries, DashboardSetup setup)
    {
        var activities = tracker.Current().Activities;
        var pending = counts.GetValueOrDefault(OperationStatus.Pending);
        var executing = counts.GetValueOrDefault(OperationStatus.Running);
        var completed = counts.GetValueOrDefault(OperationStatus.Completed);
        var providers = ProvidersOf(accountList);
        var reconciled = activities.Where(activity => activity.State == ActivityState.Healthy).Select(activity => (DateTimeOffset?)activity.UpdatedUtc).Max();
        var queued = queue.Select(operation => (DateTimeOffset?)operation.CreatedUtc).Max();
        var (done, total, rate, remaining) = ProgressOf(activities, counts);

        return new DashboardView(
            ProviderHealthOf(providerHealth),
            StageOf(activities, pending + executing, completed),
            reconciled,
            activities.Select(activity => (DateTimeOffset?)activity.UpdatedUtc).Max(),
            queued,
            done,
            total,
            rate,
            remaining,
            providers,
            pending,
            executing,
            completed,
            counts.GetValueOrDefault(OperationStatus.Failed),
            openConflicts,
            counts.GetValueOrDefault(OperationStatus.DeadLetter) + tracker.StoppedJobs(),
            providers.Count(provider => provider.Health != ProviderHealthState.Healthy),
            running,
            EventsOf(reconciled, pending + executing == 0 && completed > 0 ? queued ?? DateTimeOffset.UnixEpoch : null, scopeEntries.Select(entry => (DateTimeOffset?)entry.OccurredUtc).Max()),
            setup);
    }

    private (long Done, long? Total, double? Rate, TimeSpan? Remaining) ProgressOf(IReadOnlyList<ActivityView> activities, IReadOnlyDictionary<OperationStatus, int> counts)
    {
        var completed = counts.GetValueOrDefault(OperationStatus.Completed);
        var all = completed + counts.GetValueOrDefault(OperationStatus.Pending) + counts.GetValueOrDefault(OperationStatus.Running) + counts.GetValueOrDefault(OperationStatus.Failed) + counts.GetValueOrDefault(OperationStatus.DeadLetter);
        var perSecond = transferRate.Record(completed);
        if (!activities.Any(activity => activity.State == ActivityState.InitialIndexing)) return (completed, all, perSecond, perSecond is { } speed && all > completed ? TimeSpan.FromSeconds((all - completed) / speed) : null);

        var (rate, remaining) = CrawlEstimate(activities);

        return (activities.Sum(activity => activity.ItemsDone), activities.All(activity => activity.ItemsTotal is not null) ? activities.Sum(activity => activity.ItemsTotal!.Value) : null, rate, remaining);
    }

    private static (double? Rate, TimeSpan? Remaining) CrawlEstimate(IReadOnlyList<ActivityView> activities)
    {
        var rates = activities.Where(activity => activity.ItemsPerSecond is not null).Select(activity => activity.ItemsPerSecond!.Value).ToList();
        var remaining = activities.Where(activity => activity.EtaSeconds is not null).Select(activity => (int?)activity.EtaSeconds!.Value).Max();

        return (rates.Count == 0 ? null : rates.Sum(), remaining is { } seconds ? TimeSpan.FromSeconds(seconds) : null);
    }

    private static IReadOnlyList<string> EventsOf(DateTimeOffset? reconciled, DateTimeOffset? drained, DateTimeOffset? scopeUpdated)
        => new[] { ("ReconciliationCompleted", reconciled), ("QueueDrained", drained), ("SyncScopeUpdated", scopeUpdated) }
            .Where(item => item.Item2 is not null)
            .OrderByDescending(item => item.Item2)
            .Select(item => item.Item1)
            .ToList();

    private static IReadOnlyList<DashboardProviderView> ProvidersOf(IReadOnlyList<Account> accountList)
        => accountList.Select(account => new DashboardProviderView(account.DisplayName, account.Status == AccountStatus.ReauthenticationRequired ? ProviderHealthState.Degraded : ProviderHealthState.Healthy)).ToList();

    private async Task<Result<DashboardSetup, StoreError>> SetupAsync(IReadOnlyList<Account> accountList, CancellationToken cancellationToken)
    {
        if (accountList.Count == 0) return new Ok<DashboardSetup>(DashboardSetup.NoAccount);

        var jobList = await jobs.ListAsync(cancellationToken);
        if (jobList.Value is Fail<StoreError> jobsFailure) return jobsFailure;

        foreach (var job in ((Ok<IReadOnlyList<SyncJob>>)jobList.Value).Value)
        {
            var selected = await scopes.ListAsync(job.Id, cancellationToken);
            if (selected.Value is Fail<StoreError> scopesFailure) return scopesFailure;
            if (((Ok<IReadOnlyList<SyncScope>>)selected.Value).Value.Count > 0) return new Ok<DashboardSetup>(DashboardSetup.Ready);
        }

        return new Ok<DashboardSetup>(DashboardSetup.NoFolders);
    }

    private static DashboardStage? StageOf(IReadOnlyList<ActivityView> activities, int queued, int completed)
    {
        if (activities.Any(activity => activity.State == ActivityState.InitialIndexing)) return DashboardStage.EnumerateRemoteState;
        if (queued > 0) return DashboardStage.ExecuteQueue;
        if (activities.Any(activity => activity.State == ActivityState.Working)) return DashboardStage.AnalyseChanges;

        return completed > 0 || activities.Any(activity => activity.State == ActivityState.Healthy) ? DashboardStage.ExecuteQueue : null;
    }

    private static ProviderHealthState ProviderHealthOf(HealthStatus health) => health switch
    {
        HealthStatus.Healthy => ProviderHealthState.Healthy,
        HealthStatus.Degraded => ProviderHealthState.Degraded,
        _ => ProviderHealthState.Unhealthy
    };

    private static Fail<IpcError> Failure(StoreError error, string subject) => new(new HandlerFailed(StoreErrorMessages.Describe(error, "read", subject)));
}
