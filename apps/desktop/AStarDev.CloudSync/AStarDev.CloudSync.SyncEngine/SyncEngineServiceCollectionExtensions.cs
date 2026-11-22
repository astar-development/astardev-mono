using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Registers the sync engine.</summary>
public static class SyncEngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SyncRunner"/>. It needs <see cref="ICloudProvider"/>, <see cref="IItemIndexStore"/>, <see cref="ISyncScopeStore"/>, <see cref="ISyncJobStore"/>, <see cref="ICrawlProgressSink"/> and <see cref="TimeProvider"/>
    /// to be registered by the host; nothing runs until <see cref="SyncRunner.StartAsync"/> is called.
    /// </summary>
    /// <param name="services">The collection to add the engine to.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddSyncEngine();
    /// await provider.GetRequiredService&lt;SyncRunner&gt;().StartAsync(jobId, cancellationToken);
    /// </code>
    /// </example>
    public static IServiceCollection AddSyncEngine(this IServiceCollection services)
    {
        services.AddSingleton(provider => new InitialCrawler(
            provider.GetRequiredService<ICloudProvider>(),
            provider.GetRequiredService<IItemIndexStore>(),
            provider.GetRequiredService<ISyncScopeStore>(),
            provider.GetRequiredService<ICrawlProgressSink>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<InitialCrawler>>()));
        services.AddSingleton(provider => new DeltaProcessingService(
            provider.GetRequiredService<ICloudProvider>(),
            provider.GetRequiredService<IItemIndexStore>(),
            provider.GetRequiredService<ISyncScopeStore>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<DeltaProcessingService>>()));
        services.AddSingleton(provider => new SyncRunner(provider.GetRequiredService<ISyncJobStore>(), provider.GetRequiredService<InitialCrawler>(), provider.GetRequiredService<DeltaProcessingService>(), provider.GetRequiredService<ILogger<SyncRunner>>()));
        services.AddOptions<OperationSchedulerOptions>().BindConfiguration(OperationSchedulerOptions.ConfigurationSection);
        services.AddSingleton<OperationScheduler>();

        return services;
    }
}
