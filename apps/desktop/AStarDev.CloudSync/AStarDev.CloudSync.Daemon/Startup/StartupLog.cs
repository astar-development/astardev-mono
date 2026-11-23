using AStarDev.CloudSync.Errors;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.Startup;

internal static partial class StartupLog
{
    [LoggerMessage(EventId = 6100, Level = LogLevel.Information, Message = "Startup stage {Number}/{Total} starting: {Stage}")]
    internal static partial void StageStarting(ILogger logger, int number, int total, string stage);

    [LoggerMessage(EventId = 6101, Level = LogLevel.Critical, Message = "Startup stage {Number}/{Total} failed: {Stage}: {Cause} ({ErrorCode}). Stopping.")]
    internal static partial void StageFailed(ILogger logger, Exception? exception, int number, int total, string stage, string cause, ErrorCode errorCode);

    [LoggerMessage(EventId = 6102, Level = LogLevel.Information, Message = "Startup stage {Number}/{Total} complete: {Stage}")]
    internal static partial void StageComplete(ILogger logger, int number, int total, string stage);

    [LoggerMessage(EventId = 6103, Level = LogLevel.Warning, Message = "{Count} operations were interrupted by the last shutdown and were requeued.")]
    internal static partial void OperationsRequeued(ILogger logger, int count);

    [LoggerMessage(EventId = 6104, Level = LogLevel.Warning, Message = "The database is in recovery mode, so stored accounts are not reconnected")]
    internal static partial void AccountsNotReconnected(ILogger logger);

    [LoggerMessage(EventId = 6105, Level = LogLevel.Warning, Message = "Account {AccountId} must sign in again: {Reason} ({ErrorCode})")]
    internal static partial void AccountMustSignInAgain(ILogger logger, Guid accountId, string reason, ErrorCode errorCode);

    [LoggerMessage(EventId = 6106, Level = LogLevel.Warning, Message = "Account {AccountId} could not be reconnected now and keeps its status: {Message} ({ErrorCode})")]
    internal static partial void AccountReconnectDeferred(ILogger logger, Guid accountId, string message, ErrorCode errorCode);

    [LoggerMessage(EventId = 6107, Level = LogLevel.Warning, Message = "The open conflict count could not be read: {Message} ({ErrorCode})")]
    internal static partial void ConflictCountUnreadable(ILogger logger, string message, ErrorCode errorCode);

    [LoggerMessage(EventId = 6108, Level = LogLevel.Warning, Message = "Job {Job} could not be resumed: {Message} ({ErrorCode})")]
    internal static partial void JobResumeFailed(ILogger logger, Guid job, string message, ErrorCode errorCode);

    [LoggerMessage(EventId = 6109, Level = LogLevel.Warning, Message = "Job {Job} local observation could not be started: {Message} ({ErrorCode})")]
    internal static partial void JobObservationFailed(ILogger logger, Guid job, string message, ErrorCode errorCode);
}
