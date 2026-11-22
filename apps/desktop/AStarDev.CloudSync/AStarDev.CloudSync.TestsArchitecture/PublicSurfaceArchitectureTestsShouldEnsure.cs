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
            "Account", "AccountId", "AccountStatus", "BootstrapMode", "CanonicalItemState", "Conflict", "ConflictId", "ConflictStatus", "ConflictType", "ContentFingerprint", "CTag", "DeltaToken", "ETag", "ItemId", "ItemType", "KnownSnapshot", "LocalIdentity", "LocalSnapshot", "Operation", "OperationId", "OperationPriority", "OperationStatus", "OperationType", "RelativePath", "RemoteItemId", "RemoteSnapshot", "SnapshotId", "SyncJob", "SyncJobId", "SyncScope");

    [Fact]
    public void the_ports_expose_only_their_approved_public_types()
        => ShouldExposeExactly(typeof(ICloudProvider).Assembly,
            "AccountAlreadyExists", "AccountNotFound", "AuthenticationFailed", "ChangeKind", "ConflictNotFound", "CrawlCheckpoint", "IAccountStore", "ICloudProvider", "IConflictStore", "IEventJournal", "IItemIndexStore", "IItemStateStore", "IKnownSnapshotStore", "ILocalObservationStore", "InvalidDeltaToken", "IOperationStore", "JournalEntry", "KnownEntry", "ISyncJobStore", "ISyncScopeStore", "OperationNotDeadLettered", "OperationNotRunning", "Permanent", "ProviderAccount",
            "ProviderChange", "ProviderDeltaPage", "ProviderError", "ProviderItem", "ProviderSnapshotPage", "RateLimited", "StorageFailed", "StoredItem", "StoreError", "SyncJobAlreadyExists", "SyncJobNotFound", "UploadRequest",
            "SyncScopeAlreadyExists", "SyncScopeNotFound", "Transient");

    [Fact]
    public void ipc_exposes_only_its_approved_public_types_and_no_transport()
        => ShouldExposeExactly(typeof(IPC.IIpcServer).Assembly,
            [
             "AccountConnected", "AccountDisconnected", "AccountState", "AccountView", "AccountsView", "ActivitiesView", "ActivityChanged", "ActivityState", "ActivityView", "AddSyncScope", "CommandOutcome", "CommandResult", "ConflictChoice", "ConflictDetected", "ConflictResolved", "ConflictSideView", "ConflictView", "ConflictsView", "ConnectAccount",
             "ConnectionFailed", "ConnectionLost", "CrawlProgressChanged", "CreateSyncJob", "DaemonState", "DaemonStatusChanged", "DaemonStatusView", "DeadLetterView", "DeadLettersView", "DisconnectAccount", "FrameTooLarge",
             "GetAccounts", "GetActivities", "GetConflicts", "GetDaemonStatus", "GetDeadLetters", "GetRemoteFolders", "GetRemoteRootSummary", "GetSyncJobs", "GetSyncScopes", "HandlerFailed", "ICommand",
             "IEvent", "IgnoreOperation", "IIpcClient", "IIpcServer", "IpcError", "IQuery`1", "ListenFailed", "MalformedFrame", "NotConnected", "RemoteFolderView", "RemoteFoldersView", "RemoteRootSummaryView",
             "RemoveSyncScope", "ResolveConflict", "RetryOperation", "StartSync", "StopSync", "SyncBootstrapMode", "SyncJobView", "SyncJobsView", "SyncScopeView", "SyncScopesView", "UnixSocketIpc"]);

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
        => ShouldExposeExactly(typeof(SyncEngine.SyncRunner).Assembly, "CanonicalStateReader", "ConflictItemBusy", "ConflictItemGone", "ConflictNotOpen", "ConflictResolution", "ConflictResolver", "IConflictEventSink", "ICrawlProgressSink", "CrawlProgress", "DeltaPollingOptions", "IOperationExecutor", "KeepBothCopyFailed", "LocalObservationOptions", "LocalObservationRunner", "OperationScheduler", "OperationSchedulerOptions", "PlanSummary", "ResolutionError", "ResolutionStoreFailed", "SyncEngineServiceCollectionExtensions", "SyncPlanner", "SyncRunner");

    [Fact]
    public void single_instance_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(SingleInstance.ISingleInstanceGuard).Assembly, "InstanceClaim", "ISingleInstanceGuard", "PrimaryInstance", "SingleInstanceOptions", "SingleInstanceOptionsFactory", "SingleInstanceServiceCollectionExtensions");

    [Fact]
    public void the_functional_paradigm_library_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(Try).Assembly,
            "Exceptional", "Exceptional`1", "ExceptionalExtensions", "Fail`1", "Failure`1", "MapErrorExtensions",
            "None`1", "Ok`1", "Option", "Option`1", "OptionExtensions", "Result`2", "Some`1", "Success`1", "Try", "Unit");

    private static void ShouldExposeExactly(Assembly assembly, params string[] approvedTypeNames)
        => assembly.GetExportedTypes()
            .Where(type => type.DeclaringType is null && !type.IsDefined(typeof(CompilerGeneratedAttribute)))
            .Select(type => type.Name)
            .Order()
            .ShouldBe(approvedTypeNames.Order());
}
