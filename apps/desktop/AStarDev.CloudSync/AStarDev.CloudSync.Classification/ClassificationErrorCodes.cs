using AStarDev.CloudSync.Errors;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification;

internal static class ClassificationErrorCodes
{
    public static readonly ErrorCode InvalidSeed = Of("CS-CLASSIFY-0001");

    private static ErrorCode Of(string value) => ErrorCode.Create(value).Value is Ok<ErrorCode> ok ? ok.Value : default;
}
