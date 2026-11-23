using AStarDev.CloudSync.Errors;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.SyncEngine;

internal static class SyncEngineErrorCodes
{
    public static readonly ErrorCode ScopeNotReady = Of("CS-SYNC-0001");
    public static readonly ErrorCode DeltaProviderFailed = Of("CS-SYNC-0002");
    public static readonly ErrorCode DeltaStoreFailed = Of("CS-SYNC-0003");
    public static readonly ErrorCode ReindexFailed = Of("CS-SYNC-0004");
    public static readonly ErrorCode FileSystemFailed = Of("CS-SYNC-0005");
    public static readonly ErrorCode ObservationStoreFailed = Of("CS-SYNC-0006");
    public static readonly ErrorCode ConflictNotOpen = Of("CS-SYNC-0007");
    public static readonly ErrorCode ConflictItemGone = Of("CS-SYNC-0008");
    public static readonly ErrorCode ConflictItemBusy = Of("CS-SYNC-0009");
    public static readonly ErrorCode KeepBothCopyFailed = Of("CS-SYNC-0010");
    public static readonly ErrorCode ResolutionStoreFailed = Of("CS-SYNC-0011");
    public static readonly ErrorCode PathOutsideRoot = Of("CS-SYNC-0012");

    private static ErrorCode Of(string value) => ErrorCode.Create(value).Value is Ok<ErrorCode> ok ? ok.Value : default;
}
