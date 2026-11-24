using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Classification;

internal static partial class ClassificationLog
{
    [LoggerMessage(EventId = 9000, Level = LogLevel.Information, Message = "The classification seed was applied")]
    internal static partial void SeedApplied(ILogger logger);

    [LoggerMessage(EventId = 9001, Level = LogLevel.Information, Message = "The classification seed is already current")]
    internal static partial void SeedAlreadyCurrent(ILogger logger);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Warning, Message = "The classification seed was not applied: {Reason}")]
    internal static partial void SeedFailed(ILogger logger, string reason);
}
