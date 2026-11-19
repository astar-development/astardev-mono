namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class ProviderArchitectureTestsShouldEnsure
{
    [Fact]
    public void the_syncengine_does_not_depend_on_microsoft_graph()
    {
        var result = Types
            .InAssembly(typeof(SyncEngine.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.Graph")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void the_onedrive_provider_does_not_depend_on_the_syncengine()
    {
        var result = Types
            .InAssembly(typeof(OneDrive.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.SyncEngine")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}
