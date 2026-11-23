using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ActivityTrackerShould
{
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);
    private static readonly SyncJobId JobId = new(Guid.NewGuid());
    private static readonly RemoteItemId Docs = new("docs");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeJobs : ISyncJobStore
    {
        public Task<Result<Unit, StoreError>> AddAsync(SyncJob job, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<SyncJob, StoreError>> GetAsync(SyncJobId id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<Result<SyncJob, StoreError>>(new Ok<SyncJob>(new SyncJob(id, new AccountId(Guid.NewGuid()), "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), BootstrapMode.Download, true, Start, Start)));
        }

        public Task<Result<IReadOnlyList<SyncJob>, StoreError>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    [Fact]
    public async Task report_progress_and_eta_for_a_crawl()
    {
        var clock = new ManualTimeProvider(Start);
        var tracker = new ActivityTracker(new FakeJobs(), clock);
        await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 1000, false, 10000), Token);
        clock.Advance(TimeSpan.FromSeconds(10));

        var activity = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 2000, false, 10000), Token);

        activity.ShouldBe(new ActivityView(JobId.Value, "OneDrive", ActivityState.InitialIndexing, 2000, 10000, 20, 80, Start, Start.AddSeconds(10), null, false, 100));
    }

    [Fact]
    public async Task report_working_while_deltas_are_being_applied()
    {
        var tracker = new ActivityTracker(new FakeJobs(), new ManualTimeProvider(Start));
        await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 500, true, 500), Token);

        var working = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 500, false, Delta: true), Token);
        var done = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 500, true, Delta: true), Token);

        working.State.ShouldBe(ActivityState.Working);
        working.ItemsDone.ShouldBe(500);
        done.State.ShouldBe(ActivityState.Healthy);
    }

    [Fact]
    public async Task report_healthy_once_every_scope_has_completed()
    {
        var clock = new ManualTimeProvider(Start);
        var tracker = new ActivityTracker(new FakeJobs(), clock);

        var activity = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 500, true, 500), Token);

        activity.State.ShouldBe(ActivityState.Healthy);
        activity.Percent.ShouldBe(100);
    }

    [Fact]
    public async Task report_no_percent_or_eta_when_the_total_is_unknown()
    {
        var clock = new ManualTimeProvider(Start);
        var tracker = new ActivityTracker(new FakeJobs(), clock);
        await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 100, false), Token);
        clock.Advance(TimeSpan.FromSeconds(5));

        var activity = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 300, false), Token);

        activity.ItemsDone.ShouldBe(300);
        activity.ItemsTotal.ShouldBeNull();
        activity.Percent.ShouldBeNull();
        activity.EtaSeconds.ShouldBeNull();
    }

    [Fact]
    public async Task move_the_eta_within_a_minute_of_a_stall_then_a_burst()
    {
        var clock = new ManualTimeProvider(Start);
        var tracker = new ActivityTracker(new FakeJobs(), clock);
        await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 0, false, 10000), Token);
        clock.Advance(TimeSpan.FromSeconds(60));
        await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 600, false, 10000), Token);
        clock.Advance(TimeSpan.FromSeconds(60));
        var stalled = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 600, false, 10000), Token);
        clock.Advance(TimeSpan.FromSeconds(30));
        var burst = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 4600, false, 10000), Token);

        stalled.EtaSeconds.ShouldBeNull();
        burst.ItemsPerSecond.ShouldBe(4000d / 30);
        burst.EtaSeconds.ShouldBe(41);
    }

    [Fact]
    public async Task combine_every_scope_into_one_job_eta()
    {
        var clock = new ManualTimeProvider(Start);
        var tracker = new ActivityTracker(new FakeJobs(), clock);
        await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 0, false, 1000), Token);
        await tracker.ApplyAsync(new CrawlProgress(JobId, new RemoteItemId("pics"), 500, false, 1000), Token);
        clock.Advance(TimeSpan.FromSeconds(10));
        await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 100, false, 1000), Token);

        var activity = await tracker.ApplyAsync(new CrawlProgress(JobId, new RemoteItemId("pics"), 600, false, 1000), Token);

        activity.ItemsPerSecond.ShouldBe(20);
        activity.EtaSeconds.ShouldBe(65);
    }

    [Fact]
    public async Task show_no_eta_while_waiting_and_resume_it_after_the_wait()
    {
        var clock = new ManualTimeProvider(Start);
        var tracker = new ActivityTracker(new FakeJobs(), clock);
        await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 0, false, 10000), Token);
        clock.Advance(TimeSpan.FromSeconds(10));
        var before = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 1000, false, 10000), Token);
        clock.Advance(TimeSpan.FromSeconds(20));

        var waiting = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 1000, false, RetryIn: TimeSpan.FromSeconds(20)), Token);
        clock.Advance(TimeSpan.FromSeconds(20));
        var resumed = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 2000, false, 10000), Token);

        before.EtaSeconds.ShouldBe(90);
        waiting.EtaSeconds.ShouldBeNull();
        waiting.ItemsPerSecond.ShouldBeNull();
        resumed.EtaSeconds.ShouldBe(200);
    }

    [Fact]
    public async Task report_waiting_with_retry_seconds_when_rate_limited()
    {
        var tracker = new ActivityTracker(new FakeJobs(), new ManualTimeProvider(Start));
        await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 1000, false, 10000), Token);

        var waiting = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 1000, false, RetryIn: TimeSpan.FromSeconds(27)), Token);
        var resumed = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 1100, false, 10000), Token);

        waiting.State.ShouldBe(ActivityState.Waiting);
        waiting.RetryInSeconds.ShouldBe(27);
        waiting.ItemsDone.ShouldBe(1000);
        resumed.State.ShouldBe(ActivityState.InitialIndexing);
        resumed.RetryInSeconds.ShouldBeNull();
    }

    [Fact]
    public async Task flag_action_required_when_a_scope_fails()
    {
        var tracker = new ActivityTracker(new FakeJobs(), new ManualTimeProvider(Start));
        await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 1000, false, 10000), Token);

        var activity = await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 0, false, Failed: true), Token);

        activity.ActionRequired.ShouldBeTrue();
        activity.ItemsDone.ShouldBe(1000);
    }

    [Fact]
    public async Task list_every_reported_job_and_nothing_before_the_first_report()
    {
        var tracker = new ActivityTracker(new FakeJobs(), new ManualTimeProvider(Start));
        tracker.Current().Activities.ShouldBeEmpty();

        await tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 10, false, 100), Token);

        tracker.Current().Activities.Select(activity => activity.SyncJobId).ShouldBe([JobId.Value]);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var tracker = new ActivityTracker(new FakeJobs(), new ManualTimeProvider(Start));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => tracker.ApplyAsync(new CrawlProgress(JobId, Docs, 1, false), cancelled.Token));
    }
}
