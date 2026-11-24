using System.Xml.Linq;
using AStarDev.CloudSync.Classification;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class ClassificationArchitectureTestsShouldEnsure
{
    [Fact]
    public void not_reference_persistence_or_avalonia()
        => Types
            .InAssembly(typeof(ClassificationServiceCollectionExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AStarDev.CloudSync.Daemon",
                "AStarDev.CloudSync.IPC",
                "AStarDev.CloudSync.Observability",
                "AStarDev.CloudSync.OneDrive",
                "AStarDev.CloudSync.Persistence",
                "AStarDev.CloudSync.SyncEngine",
                "AStarDev.CloudSync.UI",
                "Microsoft.EntityFrameworkCore",
                "Avalonia")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void the_classification_project_file_references_only_domain_errors_ports_and_the_functional_paradigm_library()
    {
        var project = XDocument.Load(ProjectFile());

        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order();

        references.ShouldBe(["AStarDev.CloudSync.Domain", "AStarDev.CloudSync.Errors", "AStarDev.CloudSync.Ports", "AStarDev.FunctionalParadigm"]);
    }

    [Fact]
    public void only_the_daemon_may_depend_on_classification()
        => Types
            .InAssemblies([typeof(ItemId).Assembly, typeof(ICloudProvider).Assembly, typeof(Persistence.DatabaseOptions).Assembly, typeof(IPC.IIpcServer).Assembly, typeof(OneDrive.OneDriveServiceCollectionExtensions).Assembly, typeof(Observability.CorrelationScope).Assembly])
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.Classification")
            .GetResult().IsSuccessful.ShouldBeTrue();

    private static string ProjectFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AStarDev.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("AStarDev.slnx was not found above the test output directory");

        return Path.Combine(directory.FullName, "apps", "desktop", "AStarDev.CloudSync", "AStarDev.CloudSync.Classification", "AStarDev.CloudSync.Classification.csproj");
    }
}
