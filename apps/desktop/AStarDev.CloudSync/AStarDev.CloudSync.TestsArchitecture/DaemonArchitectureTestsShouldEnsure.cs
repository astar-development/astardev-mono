using AStarDev.CloudSync.UI.ViewModels;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class DaemonArchitectureTestsShouldEnsure
{
    [Fact]
    public void the_ui_does_not_reference_the_daemon() =>
        Types
            .InAssembly(typeof(MainViewModel).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.Daemon")
            .GetResult().IsSuccessful.ShouldBeTrue();
}
