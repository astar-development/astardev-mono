using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class ArchitectureTestsShouldEnsure
{
    [Fact]
    public void the_domain_does_not_depend_on_infrastructure()
    {
        var result = Types
            .InAssembly(typeof(ItemId).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AStarDev.CloudSync.Daemon",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Graph",
                "Avalonia",
                "Microsoft.Extensions.Hosting",
                "OpenTelemetry")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}
