using AStarDev.CloudSync.Errors;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Persistence;

internal static partial class PersistenceLog
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Warning, Message = "Dropped {Count} telemetry entries because telemetry.db could not be written: {Reason} ({ErrorCode})")]
    internal static partial void TelemetryBatchDropped(ILogger logger, int count, string reason, ErrorCode errorCode);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "Telemetry pruning failed and will be retried: {Reason} ({ErrorCode})")]
    internal static partial void TelemetryPruneFailed(ILogger logger, string reason, ErrorCode errorCode);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Debug, Message = "Pruned {Count} telemetry entries older than {Days} days")]
    internal static partial void TelemetryPruned(ILogger logger, int count, int days);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information, Message = "Database {Database} passed its integrity check and is up to date")]
    internal static partial void DatabaseReady(ILogger logger, string database);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Critical, Message = "Database {Database} failed its integrity check, entering recovery mode and leaving the file untouched: {Reason} ({ErrorCode})")]
    internal static partial void DatabaseCorrupt(ILogger logger, string database, string reason, ErrorCode errorCode);
}
