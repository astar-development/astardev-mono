using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>
/// Turns the crawl's raw progress into the <see cref="ActivityView"/> of each job, working out percentage and time left here so no client has to. Keeps what it knows in memory only:
/// after a restart it starts empty and fills as the crawl reports again.
/// </summary>
internal sealed class ActivityTracker(ISyncJobStore jobs, IAccountStore accounts, TimeProvider timeProvider)
{
    private static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(60);

    private readonly Dictionary<SyncJobId, JobState> states = [];

    /// <summary>Records one progress report and returns the job's activity after it.</summary>
    /// <param name="progress">The report. A report with <see cref="CrawlProgress.RetryIn"/> makes the job <see cref="ActivityState.Waiting"/>; one with <see cref="CrawlProgress.Failed"/> sets <see cref="ActivityView.ActionRequired"/>, and also <see cref="ActivityView.SignInRequired"/> when the job's account must sign in again.</param>
    /// <param name="cancellationToken">Cancelling abandons the lookup of the job's name and account and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>The activity of the job the report belongs to. The job's name is looked up once; if the job cannot be read the name is empty.</returns>
    public async Task<ActivityView> ApplyAsync(CrawlProgress progress, CancellationToken cancellationToken)
    {
        var name = await NameOfAsync(progress.SyncJobId, cancellationToken);
        var signInRequired = progress.Failed && await MustSignInAgainAsync(progress.SyncJobId, cancellationToken);
        lock (states)
        {
            var now = timeProvider.GetUtcNow();
            if (!states.TryGetValue(progress.SyncJobId, out var job)) states[progress.SyncJobId] = job = new JobState(name, now);

            job.Apply(progress, now);
            job.SignInRequired = signInRequired || (job.SignInRequired && job.AnyFailed);

            return job.ToView(progress.SyncJobId);
        }
    }

    /// <summary>The activity of every job reported on so far.</summary>
    /// <returns>One entry per job; empty before the first report.</returns>
    public ActivitiesView Current()
    {
        lock (states) return new ActivitiesView([.. states.Select(pair => pair.Value.ToView(pair.Key))]);
    }

    /// <summary>How many jobs currently have <see cref="ActivityView.ActionRequired"/> set, that is, stopped on an error and wait to be started again.</summary>
    /// <returns>Zero before the first report and for a job whose failed scopes have since reported without failing.</returns>
    public int StoppedJobs() => Current().Activities.Count(activity => activity.ActionRequired);

    private async Task<bool> MustSignInAgainAsync(SyncJobId id, CancellationToken cancellationToken)
        => await jobs.GetAsync(id, cancellationToken) switch
        {
            Ok<SyncJob> job => await accounts.GetAsync(job.Value.AccountId, cancellationToken) is Ok<Account> { Value.Status: AccountStatus.ReauthenticationRequired },
            Fail<StoreError> => false
        };

    private async Task<string> NameOfAsync(SyncJobId id, CancellationToken cancellationToken)
    {
        lock (states) if (states.TryGetValue(id, out var known)) return known.Name;

        return await jobs.GetAsync(id, cancellationToken) switch
        {
            Ok<SyncJob> job => job.Value.Name,
            Fail<StoreError> => string.Empty
        };
    }

    private sealed class JobState(string name, DateTimeOffset started)
    {
        private readonly Dictionary<RemoteItemId, ScopeState> scopes = [];
        private readonly Queue<(DateTimeOffset At, long Counted)> samples = [];
        private DateTimeOffset? updated;
        private TimeSpan? retryIn;

        public string Name { get; } = name;

        public bool SignInRequired { get; set; }

        public bool AnyFailed => scopes.Values.Any(scope => scope.Failed);

        public void Apply(CrawlProgress progress, DateTimeOffset now)
        {
            if (!scopes.TryGetValue(progress.RemoteRootId, out var scope)) scopes[progress.RemoteRootId] = scope = new ScopeState(progress.ItemsIndexed, now);

            scope.Failed = progress.Failed;
            retryIn = progress.RetryIn;
            if (progress.Delta) scope.Working = !progress.Completed && !progress.Failed;
            if (progress.Failed) return;

            if (progress.Delta)
            {
                scope.Completed = true;
                updated = now;

                return;
            }

            if (progress.RetryIn is null)
            {
                scope.Items = progress.ItemsIndexed;
                scope.Completed = progress.Completed;
                Sample(now);
            }

            if (progress.ItemsTotal is { } total) scope.Total = total;
            updated = now;
        }

        public ActivityView ToView(SyncJobId id)
        {
            var done = scopes.Values.Sum(scope => scope.Items);
            long? total = scopes.Values.All(scope => scope.Total is not null) ? scopes.Values.Sum(scope => scope.Total!.Value) : null;
            var state = retryIn is not null ? ActivityState.Waiting : scopes.Values.Any(scope => scope.Working) ? ActivityState.Working : scopes.Values.All(scope => scope.Completed) ? ActivityState.Healthy : ActivityState.InitialIndexing;

            var rate = state is ActivityState.InitialIndexing or ActivityState.Working ? ItemsPerSecond() : null;

            return new ActivityView(id.Value, Name, state, done, total, Percent(done, total), EtaSeconds(done, total, rate), started, updated ?? started, retryIn is { } wait ? (int)Math.Ceiling(wait.TotalSeconds) : null, AnyFailed, rate, SignInRequired && AnyFailed);
        }

        private void Sample(DateTimeOffset now)
        {
            samples.Enqueue((now, scopes.Values.Sum(scope => scope.Items - scope.Baseline)));
            while (samples.Count > 2 && now - samples.Peek().At > RateWindow) samples.Dequeue();
        }

        private double? ItemsPerSecond()
        {
            if (samples.Count < 2) return null;

            var first = samples.Peek();
            var last = samples.Last();
            var seconds = (last.At - first.At).TotalSeconds;

            return seconds > 0 ? (last.Counted - first.Counted) / seconds : null;
        }

        private static double? Percent(long done, long? total) => total is > 0 ? Math.Min(100d, Math.Round(100d * done / total.Value, 1)) : null;

        private static int? EtaSeconds(long done, long? total, double? rate) => total is { } all && rate is > 0 ? (int)Math.Ceiling(Math.Max(0, all - done) / rate.Value) : null;
    }

    private sealed class ScopeState(long baseline, DateTimeOffset firstSeen)
    {
        public long Baseline { get; } = baseline;

        public DateTimeOffset FirstSeen { get; } = firstSeen;

        public long Items { get; set; } = baseline;

        public long? Total { get; set; }

        public bool Completed { get; set; }

        public bool Failed { get; set; }

        public bool Working { get; set; }
    }
}
