using AStarDev.CloudSync.Errors;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

internal static class OneDriveErrorCodes
{
    public static readonly ErrorCode AccountNotSignedIn = Of("CS-ONEDRIVE-0001");
    public static readonly ErrorCode SecretToolDidNotStart = Of("CS-ONEDRIVE-0002");

    private static ErrorCode Of(string value) => ErrorCode.Create(value).Value is Ok<ErrorCode> ok ? ok.Value : default;
}
