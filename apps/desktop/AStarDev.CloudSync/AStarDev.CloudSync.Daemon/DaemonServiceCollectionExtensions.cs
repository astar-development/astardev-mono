using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Observability;
using AStarDev.CloudSync.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.Daemon;

/// <summary>The daemon's whole service graph in one place, so the host and the tests compose exactly the same services.</summary>
internal static class DaemonServiceCollectionExtensions
{
    /// <summary>
    /// Registers telemetry first, so everything after it is observed, then options, health checks, persistence, the startup stages and the hosted services. <see cref="StartupHostedService"/> is registered
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
        services.AddStartupStages();
        services.AddHostedService<StartupHostedService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(provider => UnixSocketIpc.CreateServer(provider.GetRequiredService<IOptions<IpcOptions>>().Value.SocketPath));
        services.AddHostedService<IpcHostedService>();

        return services;
    }
}
