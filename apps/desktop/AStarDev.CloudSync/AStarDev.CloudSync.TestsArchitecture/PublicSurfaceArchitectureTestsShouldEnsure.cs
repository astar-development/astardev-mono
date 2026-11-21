using System.Reflection;
using System.Runtime.CompilerServices;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class PublicSurfaceArchitectureTestsShouldEnsure
{
    private static readonly string[] PlaceholderAnchor = ["Class1"];

    [Fact]
    public void the_domain_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(ItemId).Assembly,
            "AccountId", "ItemId", "ItemType", "RelativePath", "RemoteItemId", "SyncScope");

    [Fact]
    public void the_ports_expose_only_their_approved_public_types()
        => ShouldExposeExactly(typeof(ICloudProvider).Assembly,
            "AuthenticationFailed", "ICloudProvider", "Permanent", "ProviderAccount", "ProviderError",
            "ProviderItem", "ProviderSnapshotPage", "RateLimited", "Transient");

    [Fact]
    public void ipc_exposes_only_its_approved_public_types_and_no_transport()
        => ShouldExposeExactly(typeof(IPC.IIpcServer).Assembly,
            [
             "CommandOutcome", "CommandResult", "ConnectionFailed", "ConnectionLost", "DaemonState", "DaemonStatusChanged",
             "DaemonStatusView", "FrameTooLarge", "GetDaemonStatus", "HandlerFailed", "ICommand", "IEvent", "IIpcClient",
             "IIpcServer", "IpcError", "IQuery`1", "ListenFailed", "MalformedFrame", "NotConnected", "UnixSocketIpc"]);

    [Fact]
    public void the_daemon_exposes_no_public_types()
        => ShouldExposeExactly(typeof(Daemon.IpcOptions).Assembly);

    [Fact]
    public void modules_without_real_members_expose_only_their_placeholder()
    {
        ShouldExposeExactly(typeof(Observability.Class1).Assembly, PlaceholderAnchor);
        ShouldExposeExactly(typeof(OneDrive.Class1).Assembly, PlaceholderAnchor);
        ShouldExposeExactly(typeof(Persistence.Class1).Assembly, PlaceholderAnchor);
        ShouldExposeExactly(typeof(SyncEngine.Class1).Assembly, PlaceholderAnchor);
    }

    [Fact]
    public void the_functional_paradigm_library_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(Try).Assembly,
            "Exceptional", "Exceptional`1", "ExceptionalExtensions", "Fail`1", "Failure`1", "MapErrorExtensions",
            "Ok`1", "Result`2", "Success`1", "Try", "Unit");

    private static void ShouldExposeExactly(Assembly assembly, params string[] approvedTypeNames)
        => assembly.GetExportedTypes()
            .Where(type => type.DeclaringType is null && !type.IsDefined(typeof(CompilerGeneratedAttribute)))
            .Select(type => type.Name)
            .Order()
            .ShouldBe(approvedTypeNames.Order());
}
