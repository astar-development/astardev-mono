using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.SyncEngine;

internal static partial class SyncLog
{
    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Sync started for job {SyncJobId}")]
    internal static partial void SyncStarted(ILogger logger, Guid syncJobId);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Sync completed for job {SyncJobId}: {ScopesCompleted} scopes indexed")]
    internal static partial void SyncCompleted(ILogger logger, Guid syncJobId, int scopesCompleted);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "Conflict {ConflictId} detected for item {ItemId} of job {SyncJobId}: {ConflictType}")]
    internal static partial void ConflictDetected(ILogger logger, Guid conflictId, Guid itemId, Guid syncJobId, string conflictType);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Information, Message = "Conflict {ConflictId} resolved for item {ItemId} of job {SyncJobId}: {Resolution}")]
    internal static partial void ConflictResolved(ILogger logger, Guid conflictId, Guid itemId, Guid syncJobId, string resolution);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Information, Message = "Delta token refreshed for {RemoteItemId} of job {SyncJobId}")]
    internal static partial void DeltaTokenRefreshed(ILogger logger, string remoteItemId, Guid syncJobId);

    [LoggerMessage(EventId = 1105, Level = LogLevel.Information, Message = "Delta processing finished for {RemoteItemId} of job {SyncJobId}: {Changes} changes applied")]
    internal static partial void DeltaProcessingFinished(ILogger logger, string remoteItemId, Guid syncJobId, int changes);

    [LoggerMessage(EventId = 1106, Level = LogLevel.Error, Message = "Delta token of {RemoteItemId} of job {SyncJobId} is invalid; listing the scope again")]
    internal static partial void DeltaTokenInvalid(ILogger logger, string remoteItemId, Guid syncJobId);

    [LoggerMessage(EventId = 1107, Level = LogLevel.Warning, Message = "Temporary file lock: {RelativePath} of job {SyncJobId} could not be read and is left for the next scan: {Reason}")]
    internal static partial void TemporaryFileLock(ILogger logger, string relativePath, Guid syncJobId, string reason);
}
