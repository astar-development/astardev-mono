using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal static partial class TelemetryTestLog
{
    [LoggerMessage(EventId = 6900, Level = LogLevel.Warning, Message = "Queued {Count} operations")]
    internal static partial void Queued(ILogger logger, int count);

    [LoggerMessage(EventId = 6901, Level = LogLevel.Error, Message = "Failure {Index}")]
    internal static partial void Failed(ILogger logger, int index);

    [LoggerMessage(EventId = 6902, Level = LogLevel.Information, Message = "Synced {Path} for {Owner} at C:\\Users\\ada\\plan.txt")]
    internal static partial void Synced(ILogger logger, string path, string owner);
}
