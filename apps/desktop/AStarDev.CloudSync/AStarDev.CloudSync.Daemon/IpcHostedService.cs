using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Serves the IPC contract. Registered after <see cref="StartupHostedService"/>, so it only listens once every recovery stage has finished.</summary>
internal sealed class IpcHostedService(IIpcServer server, ConnectAccountHandler connectAccount, DisconnectAccountHandler disconnectAccount, AccountsQuery accounts, RootSummaryQuery rootSummary, RemoteFoldersQuery remoteFolders, CreateSyncJobHandler createSyncJob, AddSyncScopeHandler addSyncScope, RemoveSyncScopeHandler removeSyncScope, SyncJobsQuery syncJobs, SyncScopesQuery syncScopes, StartSyncHandler startSync, StopSyncHandler stopSync, ActivitiesQuery activities, ConflictsQuery conflicts, DeadLettersQuery deadLetters, RetryOperationHandler retryOperation, IgnoreOperationHandler ignoreOperation, ResolveConflictHandler resolveConflict, SettingsQuery settings, SetSettingHandler setSetting, SyncStatusQuery syncStatus, HistoryQuery history, LogsQuery logs, AuditQuery audit, ExportQuery export, TimeProvider timeProvider, ILogger<IpcHostedService> logger) : IHostedService
{
    private static readonly string Version = typeof(IpcHostedService).Assembly.GetName().Version?.ToString() ?? ApplicationConstants.UnknownVersion;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await ListenAsync(cancellationToken) is Fail<StartupError> fail) throw new StartupFailedException(fail.Error);
    }

    internal async Task<Result<Unit, StartupError>> ListenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = new DaemonStatusView(DaemonState.Running, Version, timeProvider.GetUtcNow());
        server.MapQuery((GetDaemonStatus _, CancellationToken _) => AnswerStatus(status));
        server.MapCommand<ConnectAccount>(connectAccount.HandleAsync);
        server.MapCommand<DisconnectAccount>(disconnectAccount.HandleAsync);
        server.MapQuery<GetAccounts, AccountsView>(accounts.HandleAsync);
        server.MapQuery<GetRemoteRootSummary, RemoteRootSummaryView>(rootSummary.HandleAsync);
        server.MapQuery<GetRemoteFolders, RemoteFoldersView>(remoteFolders.HandleAsync);
        server.MapCommand<CreateSyncJob>(createSyncJob.HandleAsync);
        server.MapCommand<AddSyncScope>(addSyncScope.HandleAsync);
        server.MapCommand<RemoveSyncScope>(removeSyncScope.HandleAsync);
        server.MapQuery<GetSyncJobs, SyncJobsView>(syncJobs.HandleAsync);
        server.MapQuery<GetSyncScopes, SyncScopesView>(syncScopes.HandleAsync);
        server.MapCommand<StartSync>(startSync.HandleAsync);
        server.MapCommand<StopSync>(stopSync.HandleAsync);
        server.MapQuery<GetActivities, ActivitiesView>(activities.HandleAsync);
        server.MapQuery<GetConflicts, ConflictsView>(conflicts.HandleAsync);
        server.MapQuery<GetDeadLetters, DeadLettersView>(deadLetters.HandleAsync);
        server.MapCommand<RetryOperation>(retryOperation.HandleAsync);
        server.MapCommand<IgnoreOperation>(ignoreOperation.HandleAsync);
        server.MapCommand<ResolveConflict>(resolveConflict.HandleAsync);
        server.MapQuery<GetSettings, SettingsView>(settings.HandleAsync);
        server.MapCommand<SetSetting>(setSetting.HandleAsync);
        server.MapQuery<GetSyncStatus, SyncStatusView>(syncStatus.HandleAsync);
        server.MapQuery<GetHistory, HistoryView>(history.HandleAsync);
        server.MapQuery<GetLogs, LogsView>(logs.HandleAsync);
        server.MapQuery<GetAudit, AuditView>(audit.HandleAsync);
        server.MapQuery<ExportHistory, ExportView>(export.HandleAsync);
        server.MapQuery<ExportAudit, ExportView>(export.HandleAsync);

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
