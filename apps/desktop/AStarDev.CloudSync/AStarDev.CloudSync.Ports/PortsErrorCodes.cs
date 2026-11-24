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
    public static readonly ErrorCode CategoryNotFound = Of("CS-PORTS-0018");
    public static readonly ErrorCode CategoryAlreadyExists = Of("CS-PORTS-0019");
    public static readonly ErrorCode CategoryNotEmpty = Of("CS-PORTS-0020");
    public static readonly ErrorCode TermNotFound = Of("CS-PORTS-0021");
    public static readonly ErrorCode TermAlreadyExists = Of("CS-PORTS-0022");
    public static readonly ErrorCode ItemNotFound = Of("CS-PORTS-0023");
    public static readonly ErrorCode InvalidTermText = Of("CS-PORTS-0024");

    private static ErrorCode Of(string value) => ErrorCode.Create(value).Value is Ok<ErrorCode> ok ? ok.Value : default;
}
