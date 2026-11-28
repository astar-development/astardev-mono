using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Serves the IPC contract. Registered after <see cref="StartupHostedService"/>, so it only listens once every recovery stage has finished.</summary>
internal sealed class IpcHostedService(IIpcServer server, ConnectAccountHandler connectAccount, DisconnectAccountHandler disconnectAccount, AccountsQuery accounts, RootSummaryQuery rootSummary, DriveInfoQuery driveInfo, RemoteFoldersQuery remoteFolders, CreateSyncJobHandler createSyncJob, AddSyncScopeHandler addSyncScope, AddLocalFolderScopeHandler addLocalFolderScope, RemoveSyncScopeHandler removeSyncScope, SyncJobsQuery syncJobs, SyncScopesQuery syncScopes, StartSyncHandler startSync, StopSyncHandler stopSync, StopDaemonHandler stopDaemon, ActivitiesQuery activities, ConflictsQuery conflicts, DeadLettersQuery deadLetters, RetryOperationHandler retryOperation, IgnoreOperationHandler ignoreOperation, ResolveConflictHandler resolveConflict, SettingsQuery settings, SetSettingHandler setSetting, SetAccountConcurrencyHandler setAccountConcurrency, SyncStatusQuery syncStatus, DashboardSnapshotQuery dashboard, HistoryQuery history, QueueQuery queue, LogsQuery logs, TracesQuery traces, DiagnosticsQuery diagnostics, MetricsQuery metrics, ProvidersQuery providers, AuditQuery audit, ExportQuery export, TermsQuery terms, TermFilesQuery termFiles, SuggestionsQuery suggestions, StartReclassifyHandler startReclassify, ReclassifyStatusQuery reclassifyStatus, ClassificationCommands classification, TimeProvider timeProvider, ILogger<IpcHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await ListenAsync(cancellationToken) is Fail<StartupError> fail) throw new StartupFailedException(fail.Error);
    }

    internal async Task<Result<Unit, StartupError>> ListenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = new DaemonStatusView(DaemonState.Running, ApplicationConstants.DaemonVersion, timeProvider.GetUtcNow());
        server.MapQuery((GetDaemonStatus _, CancellationToken _) => AnswerStatus(status));
        server.MapCommand<ConnectAccount>(connectAccount.HandleAsync);
        server.MapCommand<DisconnectAccount>(disconnectAccount.HandleAsync);
        server.MapQuery<GetAccounts, AccountsView>(accounts.HandleAsync);
        server.MapQuery<GetRemoteRootSummary, RemoteRootSummaryView>(rootSummary.HandleAsync);
        server.MapQuery<GetDriveInfo, DriveInfoView>(driveInfo.HandleAsync);
        server.MapQuery<GetRemoteFolders, RemoteFoldersView>(remoteFolders.HandleAsync);
        server.MapCommand<CreateSyncJob>(createSyncJob.HandleAsync);
        server.MapCommand<AddSyncScope>(addSyncScope.HandleAsync);
        server.MapCommand<AddLocalFolderScope>(addLocalFolderScope.HandleAsync);
        server.MapCommand<RemoveSyncScope>(removeSyncScope.HandleAsync);
        server.MapQuery<GetSyncJobs, SyncJobsView>(syncJobs.HandleAsync);
        server.MapQuery<GetSyncScopes, SyncScopesView>(syncScopes.HandleAsync);
        server.MapCommand<StartSync>(startSync.HandleAsync);
        server.MapCommand<StopSync>(stopSync.HandleAsync);
        server.MapCommand<StopDaemon>(stopDaemon.HandleAsync);
        server.MapQuery<GetActivities, ActivitiesView>(activities.HandleAsync);
        server.MapQuery<GetConflicts, ConflictsView>(conflicts.HandleAsync);
        server.MapQuery<GetDeadLetters, DeadLettersView>(deadLetters.HandleAsync);
        server.MapCommand<RetryOperation>(retryOperation.HandleAsync);
        server.MapCommand<IgnoreOperation>(ignoreOperation.HandleAsync);
        server.MapCommand<ResolveConflict>(resolveConflict.HandleAsync);
        server.MapQuery<GetSettings, SettingsView>(settings.HandleAsync);
        server.MapCommand<SetSetting>(setSetting.HandleAsync);
        server.MapCommand<SetAccountConcurrency>(setAccountConcurrency.HandleAsync);
        server.MapQuery<GetSyncStatus, SyncStatusView>(syncStatus.HandleAsync);
        server.MapQuery<GetDashboardSnapshot, DashboardView>(dashboard.HandleAsync);
        server.MapQuery<GetHistory, HistoryView>(history.HandleAsync);
        server.MapQuery<GetQueue, QueueView>(queue.HandleAsync);
        server.MapQuery<GetLogs, LogsView>(logs.HandleAsync);
        server.MapQuery<GetTraces, TracesView>(traces.HandleAsync);
        server.MapQuery<GetDiagnostics, DiagnosticsView>(diagnostics.HandleAsync);
        server.MapQuery<GetMetrics, MetricsView>(metrics.HandleAsync);
        server.MapQuery<GetProviders, ProvidersView>(providers.HandleAsync);
        server.MapQuery<GetAudit, AuditView>(audit.HandleAsync);
        server.MapQuery<ExportHistory, ExportView>(export.HandleAsync);
        server.MapQuery<ExportAudit, ExportView>(export.HandleAsync);
        server.MapQuery<GetClassificationTerms, ClassificationTermsView>(terms.HandleAsync);
        server.MapQuery<GetTermFiles, TermFilesView>(termFiles.HandleAsync);
        server.MapQuery<GetClassificationSuggestions, ClassificationSuggestionsView>(suggestions.HandleAsync);
        server.MapCommand<StartReclassify>(startReclassify.HandleAsync);
        server.MapQuery<GetReclassifyStatus, ReclassifyStatusView>(reclassifyStatus.HandleAsync);
        server.MapCommand<AssignTerm>(classification.AssignAsync);
        server.MapCommand<PromoteSuggestion>(classification.PromoteAsync);
        server.MapCommand<IgnoreSuggestion>(classification.IgnoreAsync);
        server.MapCommand<CreateCategory>(classification.CreateCategoryAsync);
        server.MapCommand<RenameCategory>(classification.RenameCategoryAsync);
        server.MapCommand<DeleteCategory>(classification.DeleteCategoryAsync);
        server.MapCommand<CreateTerm>(classification.CreateTermAsync);
        server.MapCommand<UpdateTerm>(classification.UpdateTermAsync);
        server.MapCommand<DeleteTerm>(classification.DeleteTermAsync);

        return await server.StartAsync(cancellationToken) switch
        {
            Ok<Unit> ok => ok,
            Fail<IpcError> fail => new Fail<StartupError>(IpcStartupErrors.From(fail.Error))
        };
    }

    private Task<Result<DaemonStatusView, IpcError>> AnswerStatus(DaemonStatusView status)
    {
        DaemonLog.AnsweringDaemonStatus(logger, nameof(GetDaemonStatus), status.State);

        return Task.FromResult<Result<DaemonStatusView, IpcError>>(new Ok<DaemonStatusView>(status));
    }

    public async Task StopAsync(CancellationToken cancellationToken) => _ = await server.StopAsync(cancellationToken);
}
