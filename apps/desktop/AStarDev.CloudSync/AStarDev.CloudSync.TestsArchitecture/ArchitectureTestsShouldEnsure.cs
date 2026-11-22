using AStarDev.CloudSync.Daemon;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.UI.ViewModels;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class ArchitectureTestsShouldEnsure
{
    [Fact]
    public void the_domain_does_not_depend_on_infrastructure_related_packages() =>
        Types
            .InAssembly(typeof(ItemId).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Graph",
                "Avalonia",
                "Microsoft.Extensions.Hosting",
                "OpenTelemetry")
            .GetResult().IsSuccessful.ShouldBeTrue();
    [Fact]
    public void the_domain_does_not_depend_on_any_of_the_cloudsync_projects() =>
        Types
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
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void the_fake_provider_depends_only_on_the_domain_and_the_ports() =>
        Types
            .InAssembly(typeof(FakeProvider.FakeCloudProvider).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AStarDev.CloudSync.Daemon",
                "AStarDev.CloudSync.IPC",
                "AStarDev.CloudSync.Observability",
                "AStarDev.CloudSync.OneDrive",
                "AStarDev.CloudSync.Persistence",
                "AStarDev.CloudSync.SyncEngine",
                "AStarDev.CloudSync.UI")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void the_ui_does_not_depend_on_persistence() =>
        Types
            .InAssembly(typeof(MainViewModel).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.Persistence")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void onedrive_does_not_depend_on_persistence() =>
        Types
            .InAssembly(typeof(OneDrive.OneDriveServiceCollectionExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.Persistence")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void persistence_does_not_depend_on_the_sync_engine() =>
        Types
            .InAssembly(typeof(Persistence.DatabaseOptions).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.SyncEngine")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void the_sync_engine_does_not_depend_on_microsoft_graph() =>
        Types
            .InAssembly(typeof(SyncEngine.SyncRunner).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.Graph")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void onedrive_does_not_depend_on_the_sync_engine() =>
        Types
            .InAssembly(typeof(OneDrive.OneDriveServiceCollectionExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.SyncEngine")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void observability_does_not_depend_on_any_other_cloudsync_project()
        => ShouldNotDependOn(typeof(Observability.TelemetryServiceCollectionExtensions).Assembly,
            "AStarDev.CloudSync.Domain", "AStarDev.CloudSync.Daemon", "AStarDev.CloudSync.IPC",
            "AStarDev.CloudSync.OneDrive", "AStarDev.CloudSync.Persistence",
            "AStarDev.CloudSync.SyncEngine", "AStarDev.CloudSync.UI");

    [Fact]
    public void ipc_depends_on_domain_only()
        => ShouldNotDependOn(typeof(IPC.IIpcClient).Assembly,
            "AStarDev.CloudSync.Daemon", "AStarDev.CloudSync.Observability", "AStarDev.CloudSync.OneDrive",
            "AStarDev.CloudSync.Persistence", "AStarDev.CloudSync.SyncEngine", "AStarDev.CloudSync.UI");

    [Fact]
    public void the_ui_reaches_state_through_ipc_only()
        => ShouldNotDependOn(typeof(MainViewModel).Assembly,
            "AStarDev.CloudSync.SyncEngine", "AStarDev.CloudSync.OneDrive", "AStarDev.CloudSync.Domain",
            "AStarDev.CloudSync.Persistence", "AStarDev.CloudSync.Daemon");

    [Fact]
    public void the_sync_engine_is_independently_testable()
        => ShouldNotDependOn(typeof(SyncEngine.SyncRunner).Assembly,
            "AStarDev.CloudSync.Persistence", "AStarDev.CloudSync.OneDrive", "AStarDev.CloudSync.IPC",
            "AStarDev.CloudSync.Daemon", "AStarDev.CloudSync.UI");

    [Fact]
    public void onedrive_and_persistence_do_not_depend_on_ipc_ui_or_daemon()
    {
        string[] forbidden = ["AStarDev.CloudSync.IPC", "AStarDev.CloudSync.UI", "AStarDev.CloudSync.Daemon"];
        ShouldNotDependOn(typeof(OneDrive.OneDriveServiceCollectionExtensions).Assembly, forbidden);
        ShouldNotDependOn(typeof(Persistence.DatabaseOptions).Assembly, forbidden);
    }

    [Fact]
    public void nothing_depends_on_the_daemon()
    {
        foreach (var assembly in AllCloudSyncAssemblies.Where(a => a != typeof(IpcOptions).Assembly))
        {
            ShouldNotDependOn(assembly, "AStarDev.CloudSync.Daemon");
        }
    }

    [Fact]
    public void only_the_ui_depends_on_avalonia()
    {
        foreach (var assembly in AllCloudSyncAssemblies.Where(a => a != typeof(MainViewModel).Assembly))
        {
            ShouldNotDependOn(assembly, "Avalonia");
        }
    }

    [Fact]
    public void there_are_no_circular_project_references()
    {
        var byName = AllCloudSyncAssemblies.ToDictionary(a => a.GetName().Name!);
        const bool InProgress = false;
        const bool Done = true;
        var state = new Dictionary<string, bool>();

        foreach (var name in byName.Keys)
        {
            Visit(name, []);
        }

        void Visit(string name, List<string> path)
        {
            if (state.TryGetValue(name, out var visitState))
            {
                (visitState == Done).ShouldBeTrue($"Circular reference: {string.Join(" -> ", path.Append(name))}");
                return;
            }

            state[name] = InProgress;
            path.Add(name);
            foreach (var reference in byName[name].GetReferencedAssemblies().Select(r => r.Name!).Where(byName.ContainsKey))
            {
                Visit(reference, path);
            }

            path.RemoveAt(path.Count - 1);
            state[name] = Done;
        }
    }

    private static readonly System.Reflection.Assembly[] AllCloudSyncAssemblies =
    [
        typeof(ItemId).Assembly,
        typeof(IPC.IIpcClient).Assembly,
        typeof(Observability.TelemetryServiceCollectionExtensions).Assembly,
        typeof(OneDrive.OneDriveServiceCollectionExtensions).Assembly,
        typeof(Persistence.DatabaseOptions).Assembly,
        typeof(SyncEngine.SyncRunner).Assembly
    ];

    private static void ShouldNotDependOn(System.Reflection.Assembly assembly, params string[] forbidden) => Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult().IsSuccessful.ShouldBeTrue($"{assembly.GetName().Name} must not depend on: {string.Join(", ", forbidden)}");
}
