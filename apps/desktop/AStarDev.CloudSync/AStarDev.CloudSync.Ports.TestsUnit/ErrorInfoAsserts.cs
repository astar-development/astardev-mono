using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Ports.TestsUnit;

internal static class ErrorInfoAsserts
{
    public static void ShouldExposeUniqueCodesAndLinks(this IEnumerable<IErrorInfo> errors)
    {
        var all = errors.ToList();

        all.ShouldAllBe(error => error.Code.Value.StartsWith("CS-PORTS-") && error.HelpLink == ErrorLinks.Root + error.Code.Value);
        all.Select(error => error.Code.Value).Distinct().Count().ShouldBe(all.Count);
    }
}
