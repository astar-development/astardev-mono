using AStarDev.CloudSync.Errors;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI;

internal static class UiErrorCodes
{
    public static readonly ErrorCode MalformedResource = Of("CS-UI-0001");
    public static readonly ErrorCode DuplicateKey = Of("CS-UI-0002");
    public static readonly ErrorCode ForeignKey = Of("CS-UI-0003");
    public static readonly ErrorCode ScreenNotRegistered = Of("CS-UI-0004");
    public static readonly ErrorCode NotifierUnavailable = Of("CS-UI-0005");
    public static readonly ErrorCode NotifierFailed = Of("CS-UI-0006");
    public static readonly ErrorCode NoApplication = Of("CS-UI-0007");
    public static readonly ErrorCode UnknownTheme = Of("CS-UI-0008");
    public static readonly ErrorCode LocalisationResourcesInvalid = Of("CS-UI-0009");
    public static readonly ErrorCode NotifierDidNotStart = Of("CS-UI-0010");

    private static ErrorCode Of(string value) => ErrorCode.Create(value).Value is Ok<ErrorCode> ok ? ok.Value : default;
}
