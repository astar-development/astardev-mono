using AStarDev.CloudSync.Errors;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Ports;

internal static class PortsErrorCodes
{
    public static readonly ErrorCode RateLimited = Of("CS-PORTS-0001");
    public static readonly ErrorCode Transient = Of("CS-PORTS-0002");
    public static readonly ErrorCode AuthenticationFailed = Of("CS-PORTS-0003");
    public static readonly ErrorCode Permanent = Of("CS-PORTS-0004");
    public static readonly ErrorCode InvalidDeltaToken = Of("CS-PORTS-0005");
    public static readonly ErrorCode AccountNotFound = Of("CS-PORTS-0006");
    public static readonly ErrorCode AccountAlreadyExists = Of("CS-PORTS-0007");
    public static readonly ErrorCode StorageFailed = Of("CS-PORTS-0008");
    public static readonly ErrorCode SyncJobNotFound = Of("CS-PORTS-0009");
    public static readonly ErrorCode SyncJobAlreadyExists = Of("CS-PORTS-0010");
    public static readonly ErrorCode SyncScopeAlreadyExists = Of("CS-PORTS-0011");
    public static readonly ErrorCode SyncScopeNotFound = Of("CS-PORTS-0012");
    public static readonly ErrorCode OperationNotRunning = Of("CS-PORTS-0013");
    public static readonly ErrorCode OperationNotDeadLettered = Of("CS-PORTS-0014");
    public static readonly ErrorCode ConflictNotFound = Of("CS-PORTS-0015");
    public static readonly ErrorCode UnknownSetting = Of("CS-PORTS-0016");
    public static readonly ErrorCode InvalidSettingValue = Of("CS-PORTS-0017");

    private static ErrorCode Of(string value) => ErrorCode.Create(value).Value is Ok<ErrorCode> ok ? ok.Value : default;
}
