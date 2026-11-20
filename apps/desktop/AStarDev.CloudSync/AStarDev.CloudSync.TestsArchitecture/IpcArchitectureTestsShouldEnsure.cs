namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class IpcArchitectureTestsShouldEnsure
{
    [Fact]
    public void ipc_does_not_depend_on_the_ui_daemon_or_persistence()
    {
        var result = Types
            .InAssembly(typeof(IPC.IIpcServer).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AStarDev.CloudSync.UI",
                "AStarDev.CloudSync.Daemon",
                "AStarDev.CloudSync.Persistence")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}
