using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Persistence;

internal static partial class PersistenceLog
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Warning, Message = "Dropped {Count} telemetry entries because telemetry.db could not be written: {Reason}")]
    internal static partial void TelemetryBatchDropped(ILogger logger, int count, string reason);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Warning, Message = "Telemetry pruning failed and will be retried: {Reason}")]
    internal static partial void TelemetryPruneFailed(ILogger logger, string reason);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Debug, Message = "Pruned {Count} telemetry entries older than {Days} days")]
    internal static partial void TelemetryPruned(ILogger logger, int count, int days);
}
