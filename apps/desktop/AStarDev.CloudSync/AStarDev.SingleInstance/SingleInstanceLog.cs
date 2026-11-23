using Microsoft.Extensions.Logging;

namespace AStarDev.SingleInstance;

internal static partial class SingleInstanceLog
{
    [LoggerMessage(EventId = 8000, Level = LogLevel.Warning, Message = "{Location}: the hand-off socket could not be opened; second launches will not be answered.")]
    internal static partial void HandOffSocketUnavailable(ILogger logger, string location);

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "{Location}: another instance is running and was asked to show its window.")]
    internal static partial void HandedOff(ILogger logger, string location);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Warning, Message = "{Location}: another instance holds the lock but did not answer the hand-off ({ErrorType}).")]
    internal static partial void HandOffNotAnswered(ILogger logger, string location, string errorType);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Error, Message = "{Location}: the single-instance lock could not be created ({ErrorType}).")]
    internal static partial void LockUnavailable(ILogger logger, string location, string errorType);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Error, Message = "{Location}: accepting second launches failed {Count} times in a row; hand-off is no longer answered.")]
    internal static partial void AcceptingStopped(ILogger logger, string location, int count);

    [LoggerMessage(EventId = 8005, Level = LogLevel.Warning, Message = "{Location} caught {ErrorType}.")]
    internal static partial void AcceptFailed(ILogger logger, string location, string errorType);

    [LoggerMessage(EventId = 8006, Level = LogLevel.Warning, Message = "{Location} caught {ErrorType}.")]
    internal static partial void ActivationHandlerFailed(ILogger logger, string location, string errorType);
}
