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
            "Account", "AccountId", "AccountStatus", "BootstrapMode", "CanonicalItemState", "Category", "CategoryId", "Conflict", "ConflictId", "ConflictStatus", "ConflictType", "ContentFingerprint", "CTag", "DeltaToken", "ETag", "ItemId", "ItemType", "KnownSnapshot", "LocalIdentity", "LocalSnapshot", "Operation", "OperationId", "OperationPriority", "OperationStatus", "OperationType", "RelativePath", "RemoteItemId", "RemoteSnapshot", "ScopeCoverage", "SnapshotId", "SyncJob", "SyncJobId", "SyncScope", "Term", "TermId", "TermSource", "TermStatus");

    [Fact]
    public void the_ports_expose_only_their_approved_public_types()
        => ShouldExposeExactly(typeof(ICloudProvider).Assembly,
            "AccountAlreadyExists", "AccountNotFound", "AuthenticationFailed", "CategoryAlreadyExists", "CategoryNotEmpty", "CategoryNotFound", "ChangeKind", "ClassifiableFile", "ClassificationError", "ConflictNotFound", "CrawlCheckpoint", "IAccountStore", "ICategoryStore", "IClassifiableFileSource", "IClassificationStateStore", "ICloudProvider", "IConflictStore", "IEventJournal", "IFileClassificationStore", "IItemIndexStore", "IItemStateStore", "IJobAccounts", "IKnownSnapshotStore", "ILocalObservationStore", "IReclassifyRunStore", "InvalidDeltaToken", "InvalidSettingValue", "InvalidTermText", "IOperationStore", "ISettingsStore", "IProviderStateStore", "JournalEntry", "KnownEntry", "ISyncJobStore", "ISyncScopeStore", "ITelemetryStore", "ITelemetryWriter", "ITermStore", "ItemNotFound", "ItemTermLinks", "OperationNotDeadLettered", "OperationNotRunning", "Permanent", "ProviderAccount",
            "ProviderChange", "ProviderFeature", "ProviderDeltaPage", "ProviderDriveInfo", "ProviderError", "ProviderItem", "ProviderSnapshotPage", "RateLimited", "ReclassifyRun", "RemoteItemTermLinks", "SettingNames", "StorageFailed", "StoredItem", "StoreError", "SyncJobAlreadyExists", "SyncJobNotFound", "UploadRequest",
            "Page`1", "SyncScopeAlreadyExists", "SyncScopeNotFound", "TelemetryEntry", "TelemetryKind", "TelemetryQuery", "TermAlreadyExists", "TermFile", "TermFilesPage", "TermNotFound", "Transient", "UnknownSetting");

    [Fact]
    public void ipc_exposes_only_its_approved_public_types_and_no_transport()
        => ShouldExposeExactly(typeof(IPC.IIpcServer).Assembly,
            [
             "AccountConnected", "AccountDisconnected", "AccountState", "AccountView", "AccountsView", "ActivitiesView", "ActivityChanged", "ActivityState", "ActivityView", "AddSyncScope", "AssignTerm", "AuditEntryView", "AuditView", "ClassificationCategoryView", "ClassificationSuggestionView", "ClassificationSuggestionsView", "ClassificationTermSort", "ClassificationTermView", "ClassificationTermsView", "CommandOutcome", "CommandResult", "ConflictChoice", "ConflictDetected", "ConflictResolved", "ConflictSideView", "ConflictView", "ConflictsView", "ConnectAccount",
             "ConnectionFailed", "ConnectionLost", "CrawlPhase", "CrawlProgressChanged", "CreateCategory", "CreateSyncJob", "CreateTerm", "DeleteCategory", "DeleteTerm", "DaemonState", "DaemonStatusChanged", "DaemonStatusView", "DashboardChanged", "DashboardOperationView", "DashboardProviderView", "DashboardSetup", "DashboardStage", "DashboardView", "DeadLetterView", "DeadLettersView", "DiagnosticsView", "DisconnectAccount", "DriveInfoView", "ExportAudit", "ExportHistory", "ExportView", "FrameTooLarge",
             "GetAccounts", "GetProviders", "PromoteSuggestion", "ProviderCapability", "ProviderInsightsView", "ProviderView", "ProvidersView", "GetActivities", "GetAudit", "GetClassificationSuggestions", "GetClassificationTerms", "GetReclassifyStatus", "GetTermFiles", "GetConflicts", "GetDaemonStatus", "GetDashboardSnapshot", "GetDeadLetters", "GetDiagnostics", "GetDriveInfo", "GetHistory", "GetLogs", "GetQueue", "GetMetrics", "GetRemoteFolders", "GetRemoteRootSummary", "GetSettings", "GetSyncJobs", "GetSyncScopes", "GetSyncStatus", "GetTraces", "HandlerFailed", "HealthCheckView", "HistoryEntryView", "HistoryView", "QueueChanged", "QueuedOperationView", "QueueView", "ICommand",
             "IEvent", "IgnoreOperation", "IgnoreSuggestion", "IIpcClient", "IIpcServer", "IpcError", "IQuery`1", "ListenFailed", "LogEntryView", "LogsView", "MalformedFrame", "MetricSampleView", "MetricsView", "NotConnected", "ProviderHealthState", "ReclassifyProgressChanged", "ReclassifyStatusView", "RemoteFolderView", "RemoteFoldersView", "RemoteRootSummaryView",
             "RemoveSyncScope", "RenameCategory", "ResolveConflict", "RetryOperation", "SetAccountConcurrency", "SetSetting", "SettingsView", "SpanView", "StartReclassify", "StartSync", "StopDaemon", "StopSync", "SyncBootstrapMode", "SyncJobView", "SyncJobsView", "SyncScopeView", "SyncScopesView", "SyncStatusChanged", "UpdateTerm", "SyncStatusView", "TermFileView", "TermFilesView", "TracesView", "UnixSocketIpc"]);

    [Fact]
    public void classification_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(Classification.ClassificationServiceCollectionExtensions).Assembly, "ClassificationServiceCollectionExtensions", "ClassificationSuggestion", "IClassificationReclassifier", "IClassificationStartup", "IClassificationSuggestions", "IClassificationTermEditor", "ReclassifyAlreadyRunning", "ReclassifyCompleted", "ReclassifyFailed", "ReclassifyNotNeeded", "ReclassifyOutcome", "ReclassifyProgress", "SuggestionHint");

    [Fact]
    public void the_daemon_exposes_no_public_types()
        => ShouldExposeExactly(typeof(Daemon.IpcOptions).Assembly);

    [Fact]
    public void observability_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(Observability.TelemetryServiceCollectionExtensions).Assembly, "CorrelationScope", "TelemetryServiceCollectionExtensions");

    [Fact]
    public void errors_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(Errors.ErrorCode).Assembly, "ErrorCode", "ErrorInfo", "ErrorLinks", "IErrorInfo", "InvalidErrorCode");

    [Fact]
    public void the_fake_provider_exposes_no_public_types()
        => ShouldExposeExactly(typeof(FakeProvider.FakeCloudProvider).Assembly);

    [Fact]
    public void persistence_exposes_only_its_approved_public_types()
        => ShouldExposeExactly(typeof(Persistence.DatabaseOptions).Assembly,
            "DatabaseError", "DatabaseMode", "DatabaseOptions", "DatabaseUnavailable", "IDatabaseInitialiser", "IDatabaseState",
            "MigrationFailed", "PersistenceServiceCollectionExtensions", "TelemetryDatabaseOptions");

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
