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

    [Fact]
    public void the_ui_does_not_depend_on_persistence()
    {
        var result = Types
            .InAssembly(typeof(UI.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.Persistence")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void onedrive_does_not_depend_on_persistence()
    {
        var result = Types
            .InAssembly(typeof(OneDrive.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.Persistence")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void persistence_does_not_depend_on_the_sync_engine()
    {
        var result = Types
            .InAssembly(typeof(Persistence.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.SyncEngine")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void the_sync_engine_does_not_depend_on_microsoft_graph()
    {
        var result = Types
            .InAssembly(typeof(SyncEngine.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.Graph")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void onedrive_does_not_depend_on_the_sync_engine()
    {
        var result = Types
            .InAssembly(typeof(OneDrive.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn("AStarDev.CloudSync.SyncEngine")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    private static readonly System.Reflection.Assembly[] AllCloudSyncAssemblies =
    [
        typeof(ItemId).Assembly,
        typeof(Daemon.Class1).Assembly,
        typeof(IPC.Class1).Assembly,
        typeof(Observability.Class1).Assembly,
        typeof(OneDrive.Class1).Assembly,
        typeof(Persistence.Class1).Assembly,
        typeof(SyncEngine.Class1).Assembly,
        typeof(UI.Class1).Assembly
    ];

    private static void ShouldNotDependOn(System.Reflection.Assembly assembly, params string[] forbidden)
    {
        var result = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"{assembly.GetName().Name} must not depend on: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void observability_does_not_depend_on_any_other_cloudsync_project()
        => ShouldNotDependOn(typeof(Observability.Class1).Assembly,
            "AStarDev.CloudSync.Domain", "AStarDev.CloudSync.Daemon", "AStarDev.CloudSync.IPC",
            "AStarDev.CloudSync.OneDrive", "AStarDev.CloudSync.Persistence",
            "AStarDev.CloudSync.SyncEngine", "AStarDev.CloudSync.UI");

    [Fact]
    public void ipc_depends_on_domain_only()
        => ShouldNotDependOn(typeof(IPC.Class1).Assembly,
            "AStarDev.CloudSync.Daemon", "AStarDev.CloudSync.Observability", "AStarDev.CloudSync.OneDrive",
            "AStarDev.CloudSync.Persistence", "AStarDev.CloudSync.SyncEngine", "AStarDev.CloudSync.UI");

    [Fact]
    public void the_ui_reaches_state_through_ipc_only()
        => ShouldNotDependOn(typeof(UI.Class1).Assembly,
            "AStarDev.CloudSync.SyncEngine", "AStarDev.CloudSync.OneDrive", "AStarDev.CloudSync.Domain",
            "AStarDev.CloudSync.Persistence", "AStarDev.CloudSync.Daemon");

    [Fact]
    public void the_sync_engine_is_independently_testable()
        => ShouldNotDependOn(typeof(SyncEngine.Class1).Assembly,
            "AStarDev.CloudSync.Persistence", "AStarDev.CloudSync.OneDrive", "AStarDev.CloudSync.IPC",
            "AStarDev.CloudSync.Daemon", "AStarDev.CloudSync.UI");

    [Fact]
    public void onedrive_and_persistence_do_not_depend_on_ipc_ui_or_daemon()
    {
        string[] forbidden = ["AStarDev.CloudSync.IPC", "AStarDev.CloudSync.UI", "AStarDev.CloudSync.Daemon"];
        ShouldNotDependOn(typeof(OneDrive.Class1).Assembly, forbidden);
        ShouldNotDependOn(typeof(Persistence.Class1).Assembly, forbidden);
    }

    [Fact]
    public void nothing_depends_on_the_daemon()
    {
        foreach (var assembly in AllCloudSyncAssemblies.Where(a => a != typeof(Daemon.Class1).Assembly))
        {
            ShouldNotDependOn(assembly, "AStarDev.CloudSync.Daemon");
        }
    }

    [Fact]
    public void only_the_ui_depends_on_avalonia()
    {
        foreach (var assembly in AllCloudSyncAssemblies.Where(a => a != typeof(UI.Class1).Assembly))
        {
            ShouldNotDependOn(assembly, "Avalonia");
        }
    }

    [Fact]
    public void there_are_no_circular_project_references()
    {
        var byName = AllCloudSyncAssemblies.ToDictionary(a => a.GetName().Name!);
        var state = new Dictionary<string, bool>(); // false = in progress, true = done

        foreach (var name in byName.Keys)
        {
            Visit(name, []);
        }

        void Visit(string name, List<string> path)
        {
            if (state.TryGetValue(name, out var done))
            {
                done.ShouldBeTrue($"Circular reference: {string.Join(" -> ", path.Append(name))}");
                return;
            }

            state[name] = false;
            path.Add(name);
            foreach (var reference in byName[name].GetReferencedAssemblies().Select(r => r.Name!).Where(byName.ContainsKey))
            {
                Visit(reference, path);
            }

            path.RemoveAt(path.Count - 1);
            state[name] = true;
        }
    }
}
