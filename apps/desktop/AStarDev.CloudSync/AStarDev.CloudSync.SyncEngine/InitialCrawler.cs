using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>
/// Lists every selected scope of a job through <see cref="ICloudProvider"/> and stores the items page by page, with a checkpoint saved in the same write as each page.
/// A restart therefore continues from the last stored page instead of starting again, and no more than one page is ever held in memory.
/// </summary>
internal sealed class InitialCrawler(ICloudProvider provider, IItemIndexStore index, ISyncScopeStore scopes, ICrawlProgressSink progress, TimeProvider timeProvider, ILogger<InitialCrawler> logger)
{
    /// <summary>Crawls each scope of the job in order. A scope that fails keeps its checkpoint and the others still run; a scope that already completed is skipped.</summary>
    /// <param name="syncJobId">The job whose scopes to crawl.</param>
    /// <param name="cancellationToken">Cancelling stops after the current page is stored and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>How many scopes completed and how many failed, or <see cref="StorageFailed"/> when the scopes cannot be read.</returns>
    public async Task<Result<CrawlSummary, StoreError>> CrawlAsync(SyncJobId syncJobId, CancellationToken cancellationToken)
        => await scopes.ListAsync(syncJobId, cancellationToken) switch
        {
            Ok<IReadOnlyList<SyncScope>> ok => new Ok<CrawlSummary>(await CrawlScopesAsync(ok.Value, cancellationToken)),
            Fail<StoreError> fail => fail
        };

    private async Task<CrawlSummary> CrawlScopesAsync(IReadOnlyList<SyncScope> selected, CancellationToken cancellationToken)
    {
        var throttle = new CrawlProgressThrottle(ApplicationConstants.ProgressInterval, timeProvider);
        var completed = 0;

        foreach (var scope in selected)
        {
            if (await CrawlScopeAsync(scope, throttle, cancellationToken)) completed++;
        }

        return new CrawlSummary(completed, selected.Count - completed);
    }

    private async Task<bool> CrawlScopeAsync(SyncScope scope, CrawlProgressThrottle throttle, CancellationToken cancellationToken)
    {
        using var span = SyncEngineTelemetry.StartScope(scope.RemoteRootId.Value);
        var started = timeProvider.GetTimestamp();
        var succeeded = await index.GetCheckpointAsync(scope, cancellationToken) switch
        {
            Ok<CrawlCheckpoint> ok => ok.Value.Completed || await CrawlPagesAsync(scope, ok.Value, throttle, cancellationToken),
            Fail<StoreError> fail => Failed(scope, fail.Error.ToString()!)
        };
        SyncEngineTelemetry.RecordDuration(timeProvider.GetElapsedTime(started));
        if (!succeeded) await progress.ReportAsync(new CrawlProgress(scope.SyncJobId, scope.RemoteRootId, 0, false, Failed: true), cancellationToken);

        return succeeded;
    }

    private async Task<bool> CrawlPagesAsync(SyncScope scope, CrawlCheckpoint start, CrawlProgressThrottle throttle, CancellationToken cancellationToken)
    {
        var current = start;

        while (!current.Completed)
        {
            if (await CrawlPageAsync(scope, current, throttle, cancellationToken) is not Ok<CrawlCheckpoint> next) return false;

            current = next.Value;
        }

        return true;
    }

    private async Task<Result<CrawlCheckpoint, string>> CrawlPageAsync(SyncScope scope, CrawlCheckpoint current, CrawlProgressThrottle throttle, CancellationToken cancellationToken)
    {
        using var span = SyncEngineTelemetry.StartPage();
        Result<CrawlCheckpoint, string> stored = await FetchPageAsync(scope, current, cancellationToken) switch
        {
            Ok<ProviderSnapshotPage> ok => await StorePageAsync(scope, current, ok.Value, throttle, cancellationToken),
            Fail<ProviderError> fail => new Fail<string>(fail.Error.Value.ToString()!)
        };

        if (stored is Fail<string> failed) Failed(scope, failed.Error);

        return stored;
    }

    private async Task<Result<CrawlCheckpoint, string>> StorePageAsync(SyncScope scope, CrawlCheckpoint current, ProviderSnapshotPage page, CrawlProgressThrottle throttle, CancellationToken cancellationToken)
    {
        var last = page.ResumeToken is null;
        var next = new CrawlCheckpoint(page.ResumeToken, current.ItemsIndexed + page.Items.Count, last, last ? page.DeltaToken : null);

        if (await index.SavePageAsync(scope, page.Items, next, timeProvider.GetUtcNow(), cancellationToken) is Fail<StoreError> fail) return new Fail<string>(fail.Error.ToString()!);

        SyncEngineTelemetry.RecordIndexed(page.Items.Count);
        if (last || throttle.TryAdmit()) await progress.ReportAsync(new CrawlProgress(scope.SyncJobId, scope.RemoteRootId, next.ItemsIndexed, last, page.TotalItems), cancellationToken);

        return new Ok<CrawlCheckpoint>(next);
    }

    private async Task<Result<ProviderSnapshotPage, ProviderError>> FetchPageAsync(SyncScope scope, CrawlCheckpoint current, CancellationToken cancellationToken)
    {
        var root = scope.RemoteRootId;

        for (var attempt = 1; ; attempt++)
        {
            var page = await provider.GetSnapshotAsync(root, current.ResumeToken, cancellationToken);
            if (page is not Fail<ProviderError> fail || attempt == ApplicationConstants.MaxPageAttempts || RetryDelay(fail.Error, attempt) is not { } delay) return page;

            logger.LogWarning("Listing {Root} failed, retrying in {Delay} (attempt {Attempt}): {Error}", root.Value, delay, attempt, fail.Error.Value);
            await progress.ReportAsync(new CrawlProgress(scope.SyncJobId, root, current.ItemsIndexed, false, RetryIn: delay), cancellationToken);
            await Task.Delay(delay, timeProvider, cancellationToken);
        }
    }

    private static TimeSpan? RetryDelay(ProviderError error, int attempt) => error switch
    {
        RateLimited limited => limited.RetryAfter,
        Transient => ApplicationConstants.RetryBaseDelay * Math.Pow(2, attempt - 1),
        AuthenticationFailed => null,
        Permanent => null,
        InvalidDeltaToken => null
    };

    private bool Failed(SyncScope scope, string reason)
    {
        logger.LogError("Crawling {Root} of job {Job} stopped; its checkpoint is kept so the next run continues: {Reason}", scope.RemoteRootId.Value, scope.SyncJobId.Value, reason);

        return false;
    }
}
