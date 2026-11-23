using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Observability;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>
/// Brings the stored items and remote snapshots of a crawled scope up to date with the provider's delta pages. It only records remote state; no operation is created or run.
/// Each page is stored together with the token that follows it, so a failure leaves the old token and the next run asks again from there.
/// </summary>
internal sealed class DeltaProcessingService(ICloudProvider provider, IItemIndexStore index, ISyncScopeStore scopes, InitialCrawler crawler, ICrawlProgressSink progress, TimeProvider timeProvider, ILogger<DeltaProcessingService> logger)
{
    /// <summary>Processes every scope of the job in order. A scope that fails keeps its old token and the others still run.</summary>
    /// <param name="syncJobId">The job whose scopes to process.</param>
    /// <param name="cancellationToken">Cancelling stops after the current page is stored and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>Success once every scope was tried, or <see cref="StorageFailed"/> when the scopes cannot be read.</returns>
    public async Task<Result<Unit, StoreError>> ProcessJobAsync(SyncJobId syncJobId, CancellationToken cancellationToken)
        => await scopes.ListAsync(syncJobId, cancellationToken) switch
        {
            Ok<IReadOnlyList<SyncScope>> ok => await ProcessScopesAsync(ok.Value, cancellationToken),
            Fail<StoreError> fail => fail
        };

    /// <summary>Applies every pending delta page of one scope.</summary>
    /// <param name="scope">The scope to bring up to date. Its crawl must have completed and the provider must have given a delta token.</param>
    /// <param name="cancellationToken">Cancelling stops after the current page is stored and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>How many changes were applied, <see cref="ScopeNotReady"/> before the crawl completed, <see cref="DeltaProviderFailed"/>, <see cref="DeltaStoreFailed"/> or <see cref="ReindexFailed"/>. An invalid token lists this scope again through the crawler, once, and applies the deltas from the new token. Safe to call again after a failure.</returns>
    public async Task<Result<int, DeltaError>> ProcessScopeAsync(SyncScope scope, CancellationToken cancellationToken)
        => await index.GetCheckpointAsync(scope, cancellationToken) switch
        {
            Ok<CrawlCheckpoint> { Value: { Completed: true, DeltaToken: { } token } checkpoint } => await ApplyReportingAsync(scope, checkpoint, token, cancellationToken),
            Ok<CrawlCheckpoint> => new Fail<DeltaError>(new ScopeNotReady(scope)),
            Fail<StoreError> fail => new Fail<DeltaError>(new DeltaStoreFailed(fail.Error))
        };

    private async Task<Result<int, DeltaError>> ApplyReportingAsync(SyncScope scope, CrawlCheckpoint checkpoint, DeltaToken token, CancellationToken cancellationToken)
    {
        await progress.ReportAsync(new CrawlProgress(scope.SyncJobId, scope.RemoteRootId, checkpoint.ItemsIndexed, false, Delta: true), cancellationToken);
        var started = timeProvider.GetTimestamp();
        using var span = WorkflowTelemetry.StartDeltaScope(scope.SyncJobId, scope.RemoteRootId);
        using var correlation = CorrelationScope.Begin(logger, syncJobId: scope.SyncJobId.Value.ToString(), remoteItemId: scope.RemoteRootId.Value);
        var outcome = await ApplyPagesAsync(scope, token, cancellationToken);
        if (outcome is Fail<DeltaError> { Error.Value: DeltaProviderFailed { Error.Value: InvalidDeltaToken invalid } })
        {
            outcome = await ReindexAsync(scope, invalid.Code, cancellationToken);
            if (outcome is Ok<int>) SyncLog.DeltaTokenRefreshed(logger, scope.RemoteRootId.Value, scope.SyncJobId.Value);
        }

        if (outcome is Ok<int> applied)
        {
            SyncLog.DeltaProcessingFinished(logger, scope.RemoteRootId.Value, scope.SyncJobId.Value, applied.Value);
            span?.SetTag("cloudsync.delta.changes", applied.Value);
        }
        else if (outcome is Fail<DeltaError> failed) WorkflowTelemetry.Fail(span, failed.Error.Value?.GetType().Name ?? "unknown", "Delta processing stopped; the token is kept.");

        PerformanceMetrics.Shared.RecordDelta(timeProvider.GetElapsedTime(started));

        var succeeded = outcome is Ok<int>;
        await progress.ReportAsync(new CrawlProgress(scope.SyncJobId, scope.RemoteRootId, checkpoint.ItemsIndexed, succeeded, Failed: !succeeded, Delta: true), cancellationToken);

        return outcome;
    }

    private async Task<Result<int, DeltaError>> ReindexAsync(SyncScope scope, ErrorCode errorCode, CancellationToken cancellationToken)
    {
        SyncLog.DeltaTokenInvalid(logger, scope.RemoteRootId.Value, scope.SyncJobId.Value, errorCode);

        if (await index.ResetScopeAsync(scope, cancellationToken) is Fail<StoreError> reset) return new Fail<DeltaError>(new DeltaStoreFailed(reset.Error));

        if (!await crawler.CrawlScopeAsync(scope, cancellationToken)) return new Fail<DeltaError>(new ReindexFailed(scope));

        return await index.GetCheckpointAsync(scope, cancellationToken) switch
        {
            Ok<CrawlCheckpoint> { Value: { Completed: true, DeltaToken: { } fresh } } => await ApplyPagesAsync(scope, fresh, cancellationToken),
            Ok<CrawlCheckpoint> => new Fail<DeltaError>(new ReindexFailed(scope)),
            Fail<StoreError> fail => new Fail<DeltaError>(new DeltaStoreFailed(fail.Error))
        };
    }

    private async Task<Ok<Unit>> ProcessScopesAsync(IReadOnlyList<SyncScope> selected, CancellationToken cancellationToken)
    {
        foreach (var scope in selected)
        {
            if (await ProcessScopeAsync(scope, cancellationToken) is Fail<DeltaError> failed) EngineLog.DeltaProcessingStopped(logger, scope.RemoteRootId.Value, scope.SyncJobId.Value, failed.Error.Value, ErrorInfo.CodeOf(failed.Error.Value));
        }

        return new Ok<Unit>(default);
    }

    private async Task<Result<int, DeltaError>> ApplyPagesAsync(SyncScope scope, DeltaToken token, CancellationToken cancellationToken)
    {
        string? resume = null;
        var applied = 0;

        do
        {
            var next = await ApplyPageAsync(scope, token, resume, cancellationToken);
            if (next is Fail<DeltaError> failed) return failed;

            if (next is Ok<(int Applied, string? Resume)> page)
            {
                applied += page.Value.Applied;
                resume = page.Value.Resume;
            }
        }
        while (resume is not null);

        return new Ok<int>(applied);
    }

    private async Task<Result<(int Applied, string? Resume), DeltaError>> ApplyPageAsync(SyncScope scope, DeltaToken token, string? resume, CancellationToken cancellationToken)
    {
        using var span = WorkflowTelemetry.StartDeltaPage(scope.SyncJobId);
        var outcome = await provider.GetDeltasAsync(scope.RemoteRootId, token, resume, cancellationToken) switch
        {
            Ok<ProviderDeltaPage> ok => await StorePageAsync(scope, ok.Value, cancellationToken),
            Fail<ProviderError> fail => new Fail<DeltaError>(new DeltaProviderFailed(fail.Error))
        };
        if (outcome is Fail<DeltaError> failed) WorkflowTelemetry.Fail(span, failed.Error.Value?.GetType().Name ?? "unknown", "The delta page could not be fetched or stored.");

        return outcome;
    }

    private async Task<Result<(int Applied, string? Resume), DeltaError>> StorePageAsync(SyncScope scope, ProviderDeltaPage page, CancellationToken cancellationToken)
        => await index.ApplyDeltaPageAsync(scope, page.Changes, page.DeltaToken, timeProvider.GetUtcNow(), cancellationToken) switch
        {
            Ok<Unit> => new Ok<(int Applied, string? Resume)>((page.Changes.Count, page.ResumeToken)),
            Fail<StoreError> fail => new Fail<DeltaError>(new DeltaStoreFailed(fail.Error))
        };
}
