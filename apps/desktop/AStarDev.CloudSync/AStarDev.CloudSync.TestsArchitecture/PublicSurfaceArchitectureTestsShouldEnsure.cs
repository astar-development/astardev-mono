using System.Reflection;
using System.Runtime.CompilerServices;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsArchitecture;

public sealed class PublicSurfaceArchitectureTestsShouldEnsure
{
    [Fact]
    public void the_domain_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(ItemId).Assembly,
            "Account", "AccountId", "AccountStatus", "BootstrapMode", "DeltaToken", "ETag", "ItemId", "ItemType", "RelativePath", "RemoteItemId", "SnapshotId", "SyncJob", "SyncJobId", "SyncScope");

    [Fact]
    public void the_ports_expose_only_their_approved_public_types()
        => ShouldExposeExactly(typeof(ICloudProvider).Assembly,
            "AccountAlreadyExists", "AccountNotFound", "AuthenticationFailed", "CrawlCheckpoint", "IAccountStore", "ICloudProvider", "IItemIndexStore", "ISyncJobStore", "ISyncScopeStore", "Permanent", "ProviderAccount",
            "ProviderError", "ProviderItem", "ProviderSnapshotPage", "RateLimited", "StorageFailed", "StoreError", "SyncJobAlreadyExists", "SyncJobNotFound",
            "SyncScopeAlreadyExists", "SyncScopeNotFound", "Transient");

    [Fact]
    public void ipc_exposes_only_its_approved_public_types_and_no_transport()
        => ShouldExposeExactly(typeof(IPC.IIpcServer).Assembly,
            [
             "AccountConnected", "AccountDisconnected", "AccountState", "AccountView", "AccountsView", "AddSyncScope", "CommandOutcome", "CommandResult", "ConnectAccount",
             "ConnectionFailed", "ConnectionLost", "CrawlProgressChanged", "CreateSyncJob", "DaemonState", "DaemonStatusChanged", "DaemonStatusView", "DisconnectAccount", "FrameTooLarge",
             "GetAccounts", "GetDaemonStatus", "GetRemoteFolders", "GetRemoteRootSummary", "GetSyncJobs", "GetSyncScopes", "HandlerFailed", "ICommand",
             "IEvent", "IIpcClient", "IIpcServer", "IpcError", "IQuery`1", "ListenFailed", "MalformedFrame", "NotConnected", "RemoteFolderView", "RemoteFoldersView", "RemoteRootSummaryView",
             "RemoveSyncScope", "StartSync", "StopSync", "SyncBootstrapMode", "SyncJobView", "SyncJobsView", "SyncScopeView", "SyncScopesView", "UnixSocketIpc"]);

    [Fact]
    public void the_daemon_exposes_no_public_types()
        => ShouldExposeExactly(typeof(Daemon.IpcOptions).Assembly);

    [Fact]
    public void observability_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(Observability.TelemetryServiceCollectionExtensions).Assembly, "CorrelationScope", "TelemetryServiceCollectionExtensions");

    [Fact]
    public void persistence_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(Persistence.DatabaseOptions).Assembly,
            "DatabaseError", "DatabaseMode", "DatabaseOptions", "DatabaseUnavailable", "IDatabaseInitialiser", "IDatabaseState",
            "MigrationFailed", "PersistenceServiceCollectionExtensions");

    [Fact]
    public void onedrive_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(OneDrive.OneDriveServiceCollectionExtensions).Assembly, "OneDriveServiceCollectionExtensions");

    [Fact]
    public void the_sync_engine_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(SyncEngine.SyncRunner).Assembly, "ICrawlProgressSink", "CrawlProgress", "SyncEngineServiceCollectionExtensions", "SyncRunner");

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
