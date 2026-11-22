using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.IPC;

internal static partial class IpcLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "IPC connection opened on the {Side} side")]
    internal static partial void ConnectionOpened(ILogger logger, string side);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "IPC connection closed on the {Side} side")]
    internal static partial void ConnectionClosed(ILogger logger, string side);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "IPC {Kind} {MessageType} failed on the {Side} side: {Reason}")]
    internal static partial void RequestFailed(ILogger logger, string side, string kind, string messageType, string reason);
}
