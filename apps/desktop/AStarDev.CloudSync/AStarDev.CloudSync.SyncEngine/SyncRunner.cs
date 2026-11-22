using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>
/// Runs the initial crawl of a job in the background, at most one run per job, then applies the provider's changes every <see cref="DeltaPollingOptions.Interval"/> once every scope has crawled; a scope that failed is crawled again each round.
/// Stops on request or when the process shuts down.
/// </summary>
public sealed class SyncRunner : IAsyncDisposable
{
    private readonly Dictionary<SyncJobId, RunningCrawl> running = [];
    private readonly ISyncJobStore jobs;
    private readonly InitialCrawler crawler;
    private readonly DeltaProcessingService deltas;
    private readonly DeltaPollingOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<SyncRunner> logger;

    internal SyncRunner(ISyncJobStore jobs, InitialCrawler crawler, DeltaProcessingService deltas, IOptions<DeltaPollingOptions> options, TimeProvider timeProvider, ILogger<SyncRunner> logger)
    {
        this.jobs = jobs;
        this.crawler = crawler;
        this.deltas = deltas;
        this.options = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <summary>Starts crawling the job's selected scopes, continuing from the stored checkpoints. Returns before the crawl finishes; progress arrives through <see cref="ICrawlProgressSink"/>.</summary>
    /// <param name="syncJobId">The job to crawl.</param>
    /// <param name="cancellationToken">Cancelling abandons the start, not a crawl already running.</param>
    /// <returns>Success once the crawl runs, also when it was already running, so calling twice is safe; <see cref="SyncJobNotFound"/> when the job does not exist; or <see cref="StorageFailed"/>.</returns>
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
            if (running.ContainsKey(syncJobId)) return new Ok<Unit>(default);

            var cancellation = new CancellationTokenSource();
            running[syncJobId] = new RunningCrawl(cancellation, Task.Run(() => RunAsync(syncJobId, cancellation.Token), CancellationToken.None));
        }

        return new Ok<Unit>(default);
    }

    private async Task RunAsync(SyncJobId syncJobId, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var summary = await crawler.CrawlAsync(syncJobId, cancellationToken);
                logger.LogInformation("Crawl of job {Job} ended: {Summary}", syncJobId.Value, summary.Value);
                if (summary is Ok<CrawlSummary> { Value.ScopesFailed: 0 }) await deltas.ProcessJobAsync(syncJobId, cancellationToken);

                await Task.Delay(options.Interval > TimeSpan.Zero ? options.Interval : TimeSpan.FromSeconds(1), timeProvider, cancellationToken);
            }
        }
        finally
        {
            lock (running) running.Remove(syncJobId);
        }
    }

    private sealed record RunningCrawl(CancellationTokenSource Cancellation, Task Task);
}
