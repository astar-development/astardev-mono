using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.UI;

internal static partial class UiLog
{
    [LoggerMessage(EventId = 7000, Level = LogLevel.Error, Message = "Theme {Theme} could not be applied: {Reason}")]
    internal static partial void ThemeFailed(ILogger logger, string theme, string reason);
}
