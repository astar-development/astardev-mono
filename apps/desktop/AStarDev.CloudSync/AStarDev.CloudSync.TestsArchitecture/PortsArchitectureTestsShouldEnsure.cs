using System.Xml.Linq;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class PortsArchitectureTestsShouldEnsure
{
    private const string PortsProject = "AStarDev.CloudSync.Ports";
    private const string FunctionalParadigm = "AStarDev.FunctionalParadigm";

    private static readonly string[] ImplementationProjects =
    [
        "AStarDev.CloudSync.Daemon",
        "AStarDev.CloudSync.IPC",
        "AStarDev.CloudSync.Observability",
        "AStarDev.CloudSync.OneDrive",
        "AStarDev.CloudSync.Persistence",
        "AStarDev.CloudSync.SyncEngine",
        "AStarDev.CloudSync.UI"
    ];

    private static readonly string[] InfrastructurePackages =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Graph",
        "Avalonia",
        "Microsoft.Extensions.Hosting",
        "OpenTelemetry"
    ];

    [Fact]
    public void ports_depend_only_on_domain_and_the_functional_paradigm_library()
        => ShouldNotDependOn(typeof(ICloudProvider).Assembly, [.. ImplementationProjects, .. InfrastructurePackages]);

    [Fact]
    public void domain_does_not_depend_on_ports_or_the_functional_paradigm_library()
        => ShouldNotDependOn(typeof(ItemId).Assembly, PortsProject, FunctionalParadigm);

    [Fact]
    public void only_the_implementing_projects_depend_on_ports()
    {
        System.Reflection.Assembly[] cannotUsePorts =
        [
            typeof(ItemId).Assembly,
            typeof(IPC.IIpcClient).Assembly,
            typeof(Observability.TelemetryServiceCollectionExtensions).Assembly
        ];

        foreach (var assembly in cannotUsePorts)
        {
            ShouldNotDependOn(assembly, PortsProject);
        }
    }

    [Fact]
    public void the_ports_project_file_references_only_domain_and_the_functional_paradigm_library()
    {
        var project = LoadProject(PortsProject);

        ProjectReferences(project).Order().ShouldBe(["AStarDev.CloudSync.Domain", FunctionalParadigm]);
        PackageReferences(project).ShouldBeEmpty();
    }

    [Fact]
    public void the_domain_project_file_references_nothing()
    {
        var project = LoadProject("AStarDev.CloudSync.Domain");

        ProjectReferences(project).ShouldBeEmpty();
        PackageReferences(project).ShouldBeEmpty();
    }

    private static void ShouldNotDependOn(System.Reflection.Assembly assembly, params string[] forbidden)
    {
        var result = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"{assembly.GetName().Name} must not depend on: {string.Join(", ", forbidden)}");
    }

    private static XDocument LoadProject(string projectName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AStarDev.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("AStarDev.slnx was not found above the test output directory");

        return XDocument.Load(Path.Combine(directory.FullName, "apps", "desktop", "AStarDev.CloudSync", projectName, $"{projectName}.csproj"));
    }

    private static string[] ProjectReferences(XDocument project)
        => [.. project.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))];

    private static string[] PackageReferences(XDocument project)
        => [.. project.Descendants("PackageReference").Select(reference => reference.Attribute("Include")!.Value)];
}
