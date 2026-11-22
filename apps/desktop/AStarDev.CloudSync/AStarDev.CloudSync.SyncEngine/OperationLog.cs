using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.SyncEngine;

internal static partial class OperationLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "Operation {OperationId} queued: {OperationType} for item {ItemId} of job {SyncJobId}")]
    internal static partial void Queued(ILogger logger, Guid operationId, string operationType, Guid itemId, Guid syncJobId);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Operation started: {OperationType}, attempt {Attempt}")]
    internal static partial void Started(ILogger logger, string operationType, int attempt);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Operation completed: {OperationType}, attempt {Attempt}")]
    internal static partial void Completed(ILogger logger, string operationType, int attempt);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Retry scheduled after attempt {Attempt} at {RetryAtUtc:O}: {Reason}")]
    internal static partial void RetryScheduled(ILogger logger, int attempt, DateTimeOffset retryAtUtc, string reason);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "Rate limit encountered on attempt {Attempt}; retrying at {RetryAtUtc:O}")]
    internal static partial void RateLimitEncountered(ILogger logger, int attempt, DateTimeOffset retryAtUtc);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Error, Message = "Operation failed on attempt {Attempt} and was dead-lettered: {Reason}")]
    internal static partial void Failed(ILogger logger, int attempt, string reason);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Error, Message = "Authentication expired on attempt {Attempt}; the account must sign in again: {Reason}")]
    internal static partial void AuthenticationExpired(ILogger logger, int attempt, string reason);
}
