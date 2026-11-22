using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Observability;
using AStarDev.CloudSync.OneDrive;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.SingleInstance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.Daemon;

/// <summary>The daemon's whole service graph in one place, so the host and the tests compose exactly the same services.</summary>
internal static class DaemonServiceCollectionExtensions
{
    /// <summary>
    /// Registers telemetry first, so everything after it is observed, then options, health checks, persistence, the cloud provider, the sync engine, the single-instance guard, the startup stages, the IPC handlers and the hosted services. In debug builds the provider switch comes last, so <c>CloudSync:Provider=Fake</c> can replace the provider. <see cref="StartupHostedService"/> is registered
    /// before <see cref="IpcHostedService"/>, so the IPC server only listens once every startup stage has finished.
    /// </summary>
    /// <example>
    /// <code>
    /// var builder = Host.CreateApplicationBuilder(args);
    /// builder.Services.AddCloudSyncDaemon();
    /// await builder.Build().RunAsync();
    /// </code>
    /// </example>
    public static IServiceCollection AddCloudSyncDaemon(this IServiceCollection services)
    {
        services.AddCloudSyncTelemetry(ApplicationConstants.ServiceName);
        services.AddOptions<IpcOptions>().BindConfiguration(ApplicationConstants.IpcConfigurationSection);
        services.AddHealthChecks(); // checks are added by the slices that own database, provider, queue and IPC
        services.AddCloudSyncPersistence();
        services.AddOneDriveProvider();
        services.AddSingleton<ActivityTracker>();
        services.AddSingleton<ICrawlProgressSink, IpcCrawlProgressSink>();
        services.AddSingleton<ConflictViewFactory>();
        services.AddSingleton<IConflictEventSink, IpcConflictEventSink>();
        services.AddSyncEngine();
        services.AddSingleInstance();
        services.AddSingleton(provider => DaemonInstance.OptionsFor(provider.GetRequiredService<IOptions<IpcOptions>>().Value.SocketPath));
        services.AddStartupStages();
        services.AddHostedService<StartupHostedService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(provider => UnixSocketIpc.CreateServer(provider.GetRequiredService<IOptions<IpcOptions>>().Value.SocketPath, logger: provider.GetRequiredService<ILoggerFactory>().CreateLogger("AStarDev.CloudSync.IPC")));
        services.AddSingleton<ConnectAccountHandler>();
        services.AddSingleton<RootSummaryQuery>();
        services.AddSingleton<RemoteFoldersQuery>();
        services.AddSingleton<DisconnectAccountHandler>();
        services.AddSingleton<AccountsQuery>();
        services.AddSingleton<CreateSyncJobHandler>();
        services.AddSingleton<AddSyncScopeHandler>();
        services.AddSingleton<RemoveSyncScopeHandler>();
        services.AddSingleton<SyncJobsQuery>();
        services.AddSingleton<SyncScopesQuery>();
        services.AddSingleton<StartSyncHandler>();
        services.AddSingleton<StopSyncHandler>();
        services.AddSingleton<ActivitiesQuery>();
        services.AddSingleton<ConflictsQuery>();
        services.AddSingleton<DeadLettersQuery>();
        services.AddSingleton<RetryOperationHandler>();
        services.AddSingleton<IgnoreOperationHandler>();
        services.AddSingleton<ResolveConflictHandler>();
        services.AddHealthChecks().AddCheck<ProviderHealthCheck>(ApplicationConstants.ProviderHealthCheckName);
        services.AddHealthChecks().AddCheck<QueueHealthCheck>(ApplicationConstants.QueueHealthCheckName);
        services.AddHostedService<IpcHostedService>();
#if DEBUG
        services.AddDevelopmentProviderSwitch();
#endif

        return services;
    }
}
