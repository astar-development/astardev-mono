using System.Xml.Linq;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class PersistenceArchitectureTestsShouldEnsure
{
    [Fact]
    public void persistence_depends_only_on_domain_and_ports_within_cloudsync()
        => Types
            .InAssembly(typeof(Persistence.DatabaseOptions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AStarDev.CloudSync.Observability",
                "AStarDev.CloudSync.OneDrive",
                "AStarDev.CloudSync.SyncEngine",
                "AStarDev.CloudSync.IPC",
                "AStarDev.CloudSync.UI",
                "AStarDev.CloudSync.Daemon",
                "OpenTelemetry")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void the_persistence_project_file_references_only_domain_ports_errors_and_the_functional_paradigm_library()
    {
        var project = XDocument.Load(ProjectFile());

        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order();

        references.ShouldBe(["AStarDev.CloudSync.Domain", "AStarDev.CloudSync.Errors", "AStarDev.CloudSync.Ports", "AStarDev.FunctionalParadigm"]);
    }

    private static string ProjectFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AStarDev.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("AStarDev.slnx was not found above the test output directory");

        return Path.Combine(directory.FullName, "apps", "desktop", "AStarDev.CloudSync", "AStarDev.CloudSync.Persistence", "AStarDev.CloudSync.Persistence.csproj");
    }
}
