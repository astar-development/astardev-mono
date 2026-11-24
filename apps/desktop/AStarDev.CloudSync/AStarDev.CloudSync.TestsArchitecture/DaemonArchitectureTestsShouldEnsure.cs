using System.Xml.Linq;
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

    [Fact]
    public void the_daemon_project_file_references_only_the_approved_modules()
    {
        var project = XDocument.Load(ProjectFile());

        var references = project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
            .Order();

        references.ShouldBe(["AStarDev.CloudSync.Classification", "AStarDev.CloudSync.Domain", "AStarDev.CloudSync.Errors", "AStarDev.CloudSync.FakeProvider", "AStarDev.CloudSync.IPC", "AStarDev.CloudSync.Observability", "AStarDev.CloudSync.OneDrive", "AStarDev.CloudSync.Persistence", "AStarDev.CloudSync.SyncEngine", "AStarDev.FunctionalParadigm", "AStarDev.SingleInstance"]);
    }

    private static string ProjectFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AStarDev.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("AStarDev.slnx was not found above the test output directory");

        return Path.Combine(directory.FullName, "apps", "desktop", "AStarDev.CloudSync", "AStarDev.CloudSync.Daemon", "AStarDev.CloudSync.Daemon.csproj");
    }
}
