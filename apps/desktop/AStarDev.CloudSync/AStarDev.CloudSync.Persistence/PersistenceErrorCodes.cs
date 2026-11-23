using AStarDev.CloudSync.Errors;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Persistence;

internal static class PersistenceErrorCodes
{
    public static readonly ErrorCode DatabaseUnavailable = Of("CS-DB-0001");
    public static readonly ErrorCode MigrationFailed = Of("CS-DB-0002");

    private static ErrorCode Of(string value) => ErrorCode.Create(value).Value is Ok<ErrorCode> ok ? ok.Value : default;
}
