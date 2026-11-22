using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Registers the sync engine.</summary>
public static class SyncEngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SyncRunner"/>, <see cref="SyncPlanner"/>, <see cref="ConflictResolver"/> and <see cref="OperationScheduler"/>, whose executor performs downloads, uploads, renames and deletes and commits baselines only on success. A host that registers its own <see cref="IOperationExecutor"/> or <see cref="IConflictEventSink"/> before calling this replaces the built-in one; without a sink, conflict announcements go nowhere. It needs <see cref="ICloudProvider"/>, <see cref="IItemIndexStore"/>, <see cref="ISyncScopeStore"/>, <see cref="ISyncJobStore"/>, <see cref="IEventJournal"/>, <see cref="ICrawlProgressSink"/> and <see cref="TimeProvider"/>
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
            provider.GetRequiredService<InitialCrawler>(),
            provider.GetRequiredService<ICrawlProgressSink>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<DeltaProcessingService>>()));
        services.AddOptions<DeltaPollingOptions>().BindConfiguration(DeltaPollingOptions.ConfigurationSection);
        services.AddSingleton(provider => new SyncRunner(provider.GetRequiredService<ISyncJobStore>(), provider.GetRequiredService<InitialCrawler>(), provider.GetRequiredService<DeltaProcessingService>(), provider.GetRequiredService<IOptions<DeltaPollingOptions>>(), provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<SyncRunner>>()));
        services.AddOptions<LocalObservationOptions>().BindConfiguration(LocalObservationOptions.ConfigurationSection);
        services.TryAddSingleton<ILocalFileSystem, PhysicalLocalFileSystem>();
        services.AddSingleton(provider => new LocalObserver(provider.GetRequiredService<ILocalFileSystem>(), provider.GetRequiredService<ILocalObservationStore>(), provider.GetRequiredService<IOptions<LocalObservationOptions>>(), provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<LocalObserver>>()));
        services.AddSingleton(provider => new LocalObservationRunner(provider.GetRequiredService<ISyncJobStore>(), provider.GetRequiredService<LocalObserver>(), provider.GetRequiredService<ILogger<LocalObservationRunner>>()));
        services.AddOptions<OperationSchedulerOptions>().BindConfiguration(OperationSchedulerOptions.ConfigurationSection);
        services.TryAddSingleton<ILocalFileStore, PhysicalLocalFileStore>();
        services.TryAddSingleton<IOperationExecutor>(provider => new OperationExecutor(
            provider.GetRequiredService<ISyncJobStore>(),
            provider.GetRequiredService<IItemStateStore>(),
            provider.GetRequiredService<IKnownSnapshotStore>(),
            provider.GetRequiredService<ICloudProvider>(),
            provider.GetRequiredService<ILocalFileStore>()));
        services.AddSingleton(provider => new OperationScheduler(
            provider.GetRequiredService<IOperationStore>(),
            new KnownSnapshotCommitter(provider.GetRequiredService<IOperationExecutor>(), provider.GetRequiredService<IKnownSnapshotStore>()),
            provider.GetRequiredService<IOptions<OperationSchedulerOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<OperationScheduler>>()));
        services.TryAddSingleton<IConflictEventSink, NullConflictEventSink>();
        services.AddSingleton(provider => new SyncPlanner(
            provider.GetRequiredService<ISyncJobStore>(),
            provider.GetRequiredService<IItemStateStore>(),
            provider.GetRequiredService<IKnownSnapshotStore>(),
            provider.GetRequiredService<IOperationStore>(),
            provider.GetRequiredService<IConflictStore>(),
            provider.GetRequiredService<IEventJournal>(),
            provider.GetRequiredService<IConflictEventSink>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<SyncPlanner>>()));
        services.AddSingleton(provider => new ConflictResolver(
            provider.GetRequiredService<IConflictStore>(),
            provider.GetRequiredService<ISyncJobStore>(),
            provider.GetRequiredService<IItemStateStore>(),
            provider.GetRequiredService<IKnownSnapshotStore>(),
            provider.GetRequiredService<IOperationStore>(),
            provider.GetRequiredService<ILocalObservationStore>(),
            provider.GetRequiredService<ILocalFileStore>(),
            provider.GetRequiredService<IEventJournal>(),
            provider.GetRequiredService<IConflictEventSink>(),
            provider.GetRequiredService<SyncPlanner>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<ConflictResolver>>()));

        return services;
    }
}
