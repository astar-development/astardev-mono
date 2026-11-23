using AStarDev.CloudSync.Errors;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

internal static class DaemonErrorCodes
{
    public static readonly ErrorCode StartupStageFailed = Of("CS-DAEMON-0001");

    private static ErrorCode Of(string value) => ErrorCode.Create(value).Value is Ok<ErrorCode> ok ? ok.Value : default;
}
