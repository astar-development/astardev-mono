using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.SyncEngine;

internal static partial class EngineLog
{
    [LoggerMessage(EventId = 1200, Level = LogLevel.Information, Message = "Planned job {Job}: {Queued} operations queued, {Conflicts} conflicts stored")]
    internal static partial void JobPlanned(ILogger logger, Guid job, int queued, int conflicts);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "Crawl of job {Job} ended: {Summary}")]
    internal static partial void CrawlEnded(ILogger logger, Guid job, object? summary);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Warning, Message = "Planning job {Job} after keep both failed: {Error}")]
    internal static partial void PlanningAfterKeepBothFailed(ILogger logger, Guid job, object? error);

    [LoggerMessage(EventId = 1203, Level = LogLevel.Error, Message = "The operation queue could not be read or written: {Error}")]
    internal static partial void QueueUnavailable(ILogger logger, object? error);

    [LoggerMessage(EventId = 1204, Level = LogLevel.Warning, Message = "Local observation of job {Job} ended: {Failure}")]
    internal static partial void LocalObservationEnded(ILogger logger, Guid job, object? failure);

    [LoggerMessage(EventId = 1205, Level = LogLevel.Warning, Message = "Delta processing of {Root} of job {Job} stopped; its token is kept: {Reason}")]
    internal static partial void DeltaProcessingStopped(ILogger logger, string root, Guid job, object? reason);

    [LoggerMessage(EventId = 1206, Level = LogLevel.Warning, Message = "Listing {Root} failed, retrying in {Delay} (attempt {Attempt}): {Error}")]
    internal static partial void ListingRetrying(ILogger logger, string root, TimeSpan delay, int attempt, object? error);

    [LoggerMessage(EventId = 1207, Level = LogLevel.Error, Message = "Crawling {Root} of job {Job} stopped; its checkpoint is kept so the next run continues: {Reason}")]
    internal static partial void CrawlStopped(ILogger logger, string root, Guid job, string reason);

    [LoggerMessage(EventId = 1208, Level = LogLevel.Warning, Message = "Local observation of job {Job} failed and will scan the whole root at the next change: {Failure}")]
    internal static partial void LocalObservationFailed(ILogger logger, Guid job, object? failure);
}
