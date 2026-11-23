using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

internal static partial class DaemonLog
{
    [LoggerMessage(EventId = 6002, Level = LogLevel.Warning, Message = "Adding a sync scope failed: {Message} ({ErrorCode})")]
    internal static partial void AddSyncScopeFailed(ILogger logger, string message, ErrorCode errorCode);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Information, Message = "Disconnected account {AccountId}")]
    internal static partial void AccountDisconnected(ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 6004, Level = LogLevel.Warning, Message = "Disconnecting an account failed: {Message} ({ErrorCode})")]
    internal static partial void DisconnectAccountFailed(ILogger logger, string message, ErrorCode errorCode);

    [LoggerMessage(EventId = 6005, Level = LogLevel.Warning, Message = "Starting the sync failed: {Message} ({ErrorCode})")]
    internal static partial void StartSyncFailed(ILogger logger, string message, ErrorCode errorCode);

    [LoggerMessage(EventId = 6006, Level = LogLevel.Warning, Message = "Publishing {Event} failed: {Error} ({ErrorCode})")]
    internal static partial void PublishFailed(ILogger logger, string @event, object error, ErrorCode errorCode);

    [LoggerMessage(EventId = 6007, Level = LogLevel.Information, Message = "Answering {Query} with daemon state {State}")]
    internal static partial void AnsweringDaemonStatus(ILogger logger, string query, DaemonState state);

    [LoggerMessage(EventId = 6008, Level = LogLevel.Warning, Message = "Saving a setting failed: {Message} ({ErrorCode})")]
    internal static partial void SetSettingFailed(ILogger logger, string message, ErrorCode errorCode);

    [LoggerMessage(EventId = 6009, Level = LogLevel.Information, Message = "Connected account {AccountId}")]
    internal static partial void AccountConnected(ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 6010, Level = LogLevel.Warning, Message = "Connecting an account failed: {Message} ({ErrorCode})")]
    internal static partial void ConnectAccountFailed(ILogger logger, string message, ErrorCode errorCode);

    [LoggerMessage(EventId = 6011, Level = LogLevel.Information, Message = "Created sync job {Job} in {Mode} mode")]
    internal static partial void SyncJobCreated(ILogger logger, Guid job, BootstrapMode mode);

    [LoggerMessage(EventId = 6012, Level = LogLevel.Warning, Message = "Creating a sync job failed: {Message} ({ErrorCode})")]
    internal static partial void CreateSyncJobFailed(ILogger logger, string message, ErrorCode errorCode);

    [LoggerMessage(EventId = 6013, Level = LogLevel.Warning, Message = "Removing a sync scope failed: {Message} ({ErrorCode})")]
    internal static partial void RemoveSyncScopeFailed(ILogger logger, string message, ErrorCode errorCode);

    [LoggerMessage(EventId = 6030, Level = LogLevel.Warning, Message = "The drive information of the providers screen could not be read: {Message} ({ErrorCode})")]
    internal static partial void DriveInfoUnavailable(ILogger logger, string message, ErrorCode errorCode);
}
