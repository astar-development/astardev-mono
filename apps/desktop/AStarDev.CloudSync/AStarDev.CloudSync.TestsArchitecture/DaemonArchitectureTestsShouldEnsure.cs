namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class DaemonArchitectureTestsShouldEnsure
{
    [Fact]
    public void the_ui_does_not_reference_the_daemon()
    {
        var result = Types
            .InAssembly(typeof(UI.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.Daemon")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}
