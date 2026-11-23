using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class StartupErrorShould
{
    [Fact]
    public void expose_a_code_and_link()
    {
        var error = new StartupError("one", "boom");

        error.Code.Value.ShouldStartWith("CS-DAEMON-");
        error.HelpLink.ShouldBe(ErrorLinks.Root + error.Code.Value);
    }
}
