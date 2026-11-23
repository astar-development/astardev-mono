using System.Xml.Linq;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class OneDriveArchitectureTestsShouldEnsure
{
    [Fact]
    public void the_onedrive_project_file_references_only_domain_errors_ports_observability_and_the_functional_paradigm_library()
    {
        var project = XDocument.Load(ProjectFile());

        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order();

        references.ShouldBe(["AStarDev.CloudSync.Domain", "AStarDev.CloudSync.Errors", "AStarDev.CloudSync.Observability", "AStarDev.CloudSync.Ports", "AStarDev.FunctionalParadigm"]);
    }

    private static string ProjectFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AStarDev.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("AStarDev.slnx was not found above the test output directory");

        return Path.Combine(directory.FullName, "apps", "desktop", "AStarDev.CloudSync", "AStarDev.CloudSync.OneDrive", "AStarDev.CloudSync.OneDrive.csproj");
    }
}
