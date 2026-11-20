using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.Startup;

public static class StartupServiceCollectionExtensions
{
    /// <summary>Registers the six doc 14 stages. Registration order is execution order.</summary>
    public static IServiceCollection AddStartupStages(this IServiceCollection services)
    {
        services.AddSingleton<IStartupStage, ProcessStartStage>();
        services.AddSingleton<IStartupStage, InfrastructureInitialisationStage>();
        services.AddSingleton<IStartupStage, StateRecoveryStage>();
        services.AddSingleton<IStartupStage, QueueRecoveryStage>();
        services.AddSingleton<IStartupStage, BackgroundServicesStage>();
        services.AddSingleton<IStartupStage, AcceptConnectionsStage>();
        services.AddSingleton<StartupSequence>();
        return services;
    }
}
