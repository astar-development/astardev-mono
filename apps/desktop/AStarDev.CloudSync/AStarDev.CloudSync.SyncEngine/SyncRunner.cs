using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Observability;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>
/// Runs the initial crawl of a job in the background, at most one run per job, then applies the provider's changes and plans the job into operations every <see cref="DeltaPollingOptions.Interval"/> once every scope has crawled; a scope that failed is crawled again each round.
/// Starting a job that already runs wakes it, so a scope added meanwhile crawls at once. Stops on request or when the process shuts down.
/// </summary>
public sealed class SyncRunner : IAsyncDisposable
{
    private readonly Dictionary<SyncJobId, RunningCrawl> running = [];
    private readonly ISyncJobStore jobs;
    private readonly InitialCrawler crawler;
    private readonly DeltaProcessingService deltas;
    private readonly SyncPlanner planner;
    private readonly DeltaPollingOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SyncRunner> logger;

    internal SyncRunner(ISyncJobStore jobs, InitialCrawler crawler, DeltaProcessingService deltas, SyncPlanner planner, IOptions<DeltaPollingOptions> options, TimeProvider timeProvider, ILogger<SyncRunner> logger)
    {
        this.jobs = jobs;
        this.crawler = crawler;
        this.deltas = deltas;
        this.planner = planner;
        this.options = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <summary>Starts crawling the job's selected scopes, continuing from the stored checkpoints. Returns before the crawl finishes; progress arrives through <see cref="ICrawlProgressSink"/>.</summary>
    /// <param name="syncJobId">The job to crawl.</param>
    /// <param name="cancellationToken">Cancelling abandons the start, not a crawl already running.</param>
    /// <returns>Success once the crawl runs, also when it was already running, in which case the next crawl round begins at once instead of after the polling interval, so calling twice is safe; <see cref="SyncJobNotFound"/> when the job does not exist; or <see cref="StorageFailed"/>.</returns>
    public async Task<Result<Unit, StoreError>> StartAsync(SyncJobId syncJobId, CancellationToken cancellationToken)
        => await jobs.GetAsync(syncJobId, cancellationToken) switch
        {
            Ok<SyncJob> => Launch(syncJobId),
            Fail<StoreError> fail => fail
        };

    /// <summary>Stops the crawl of the job and waits for it to end. The stored checkpoint stays, so <see cref="StartAsync"/> continues where it stopped.</summary>
    /// <param name="syncJobId">The job to stop. Nothing happens when it is not running.</param>
    /// <param name="cancellationToken">Cancelling stops the waiting, not the stop; the crawl is already cancelled by then.</param>
    public async Task StopAsync(SyncJobId syncJobId, CancellationToken cancellationToken)
    {
        RunningCrawl? crawl;
        lock (running) running.TryGetValue(syncJobId, out crawl);
        if (crawl is null) return;

        await crawl.Cancellation.CancelAsync();
        await Task.WhenAny(crawl.Task).WaitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        RunningCrawl[] crawls;
        lock (running) crawls = [.. running.Values];

        foreach (var crawl in crawls) await crawl.Cancellation.CancelAsync();

        await Task.WhenAll(crawls.Select(crawl => Task.WhenAny(crawl.Task)));
    }

    private Ok<Unit> Launch(SyncJobId syncJobId)
    {
        lock (running)
        {
            if (running.TryGetValue(syncJobId, out var current))
            {
                current.Wake();

                return new Ok<Unit>(default);
            }

            SyncLog.SyncStarted(logger, syncJobId.Value);
            var cancellation = new CancellationTokenSource();
            var wake = new SemaphoreSlim(0, 1);
            running[syncJobId] = new RunningCrawl(cancellation, wake, Task.Run(() => RunAsync(syncJobId, wake, cancellation.Token), CancellationToken.None));
        }

        return new Ok<Unit>(default);
    }

    private async Task RunAsync(SyncJobId syncJobId, SemaphoreSlim wake, CancellationToken cancellationToken)
    {
        SyncMetrics.Shared.JobStarted();
        using var scope = CorrelationScope.Begin(logger, syncJobId: syncJobId.Value.ToString());
        var completedLogged = false;
        try
        {
            while (true)
            {
                var summary = await crawler.CrawlAsync(syncJobId, cancellationToken);
                EngineLog.CrawlEnded(logger, syncJobId.Value, summary.Value);
                if (summary is Ok<CrawlSummary> { Value: { ScopesFailed: 0 } crawled })
                {
                    if (!completedLogged) SyncLog.SyncCompleted(logger, syncJobId.Value, crawled.ScopesCompleted);

                    completedLogged = true;
                    await deltas.ProcessJobAsync(syncJobId, cancellationToken);
                    await PlanAsync(syncJobId, cancellationToken);
                }

                await SleepAsync(wake, cancellationToken);
            }
        }
        finally
        {
            SyncMetrics.Shared.JobStopped();
            lock (running) running.Remove(syncJobId);
        }
    }

    private async Task PlanAsync(SyncJobId syncJobId, CancellationToken cancellationToken)
    {
        if (await planner.PlanAsync(syncJobId, cancellationToken) is { Value: Fail<StoreError> failure }) EngineLog.PlanningAfterCrawlFailed(logger, syncJobId.Value, failure.Error.Value, ErrorInfo.CodeOf(failure.Error.Value));
    }

    private async Task SleepAsync(SemaphoreSlim wake, CancellationToken cancellationToken)
    {
        using var sleep = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var elapsed = Task.Delay(options.Interval > TimeSpan.Zero ? options.Interval : TimeSpan.FromSeconds(1), timeProvider, sleep.Token);
        await Task.WhenAny(elapsed, wake.WaitAsync(sleep.Token));
        await sleep.CancelAsync();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private sealed record RunningCrawl(CancellationTokenSource Cancellation, SemaphoreSlim WakeSignal, Task Task)
    {
        public void Wake()
        {
            if (WakeSignal.CurrentCount == 0) WakeSignal.Release();
        }
    }
}
