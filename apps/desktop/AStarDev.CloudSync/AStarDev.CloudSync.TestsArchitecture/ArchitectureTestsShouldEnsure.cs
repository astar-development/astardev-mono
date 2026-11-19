using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class ArchitectureTestsShouldEnsure
{
    [Fact]
    public void the_domain_does_not_depend_on_infrastructure_related_packages()
    {
        var result = Types
            .InAssembly(typeof(ItemId).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Graph",
                "Avalonia",
                "Microsoft.Extensions.Hosting",
                "OpenTelemetry")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
    [Fact]
    public void the_domain_does_not_depend_on_any_of_the_cloudsync_projects()
    {
        var result = Types
            .InAssembly(typeof(ItemId).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AStarDev.CloudSync.Daemon",
                "AStarDev.CloudSync.IPC",
                "AStarDev.CloudSync.Observability",
                "AStarDev.CloudSync.OneDrive",
                "AStarDev.CloudSync.Persistence",
                "AStarDev.CloudSync.SyncEngine",
                "AStarDev.CloudSync.UI")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}
