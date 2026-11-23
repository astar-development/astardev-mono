using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.UI.Notifications;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class DesktopNotifierErrorShould
{
    [Fact]
    public void expose_a_code_and_link_for_every_case()
    {
        IErrorInfo[] cases =
        [
            new NotifierUnavailable("none"),
            new NotifierFailed("exit 1")
        ];

        cases.ShouldAllBe(error => error.Code.Value.StartsWith("CS-UI-") && error.HelpLink == ErrorLinks.Root + error.Code.Value);
        cases.Select(error => error.Code.Value).Distinct().Count().ShouldBe(cases.Length);
    }
}
