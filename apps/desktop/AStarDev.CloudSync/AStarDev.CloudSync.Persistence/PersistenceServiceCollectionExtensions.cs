using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Registers the SQLite state store.</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DatabaseOptions"/>, the EF Core context factory, <see cref="IDatabaseInitialiser"/>, <see cref="IDatabaseState"/>, <see cref="IAccountStore"/>
    /// the telemetry store on its own <c>telemetry.db</c> (<see cref="TelemetryDatabaseOptions"/>, <see cref="ITelemetryStore"/>, <see cref="ITelemetryWriter"/>, a batching writer and an hourly pruning service)
    /// and a health check named <c>database</c>. Nothing touches the file until <see cref="IDatabaseInitialiser.InitialiseAsync"/> runs.
    /// </summary>
    /// <param name="services">The collection to add the persistence services to.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddCloudSyncPersistence();
    /// var result = await provider.GetRequiredService&lt;IDatabaseInitialiser&gt;().InitialiseAsync(cancellationToken);
    /// </code>
    /// </example>
    public static IServiceCollection AddCloudSyncPersistence(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.ConfigurationSection);
        services.AddDbContextFactory<CloudSyncDbContext>((provider, builder) => builder.UseSqlite($"Data Source={provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.Path}"));
        services.AddSingleton<DatabaseState>();
        services.AddSingleton<IDatabaseState>(provider => provider.GetRequiredService<DatabaseState>());
        services.AddSingleton<IDatabaseInitialiser, DatabaseInitialiser>();
        services.AddSingleton<IAccountStore, AccountStore>();
        services.AddSingleton<ISyncJobStore, SyncJobStore>();
        services.AddSingleton<ISyncScopeStore, SyncScopeStore>();
        services.AddSingleton<IItemIndexStore, ItemIndexStore>();
        services.AddSingleton<ILocalObservationStore, LocalObservationStore>();
        services.AddSingleton<IOperationStore, OperationStore>();
        services.AddSingleton<IItemStateStore, ItemStateStore>();
        services.AddSingleton<IKnownSnapshotStore, KnownSnapshotStore>();
        services.AddSingleton<IConflictStore, ConflictStore>();
        services.AddSingleton<IEventJournal, EventJournalStore>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<IProviderStateStore, ProviderStateStore>();
        services.AddSingleton<ICategoryStore, CategoryStore>();
        services.AddSingleton<ITermStore, TermStore>();
        services.AddSingleton<IFileClassificationStore, FileClassificationStore>();
        services.AddSingleton<IClassificationStateStore, ClassificationStateStore>();
        services.AddSingleton<IClassifiableFileSource, ClassifiableFileSource>();
        services.AddOptions<TelemetryDatabaseOptions>().BindConfiguration(TelemetryDatabaseOptions.ConfigurationSection);
        services.AddDbContextFactory<TelemetryDbContext>((provider, builder) => builder.UseSqlite($"Data Source={provider.GetRequiredService<IOptions<TelemetryDatabaseOptions>>().Value.Path}"));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITelemetryStore, TelemetryStore>();
        services.AddSingleton<TelemetryBatchWriter>();
        services.AddSingleton<ITelemetryWriter>(provider => provider.GetRequiredService<TelemetryBatchWriter>());
        services.AddHostedService(provider => provider.GetRequiredService<TelemetryBatchWriter>());
        services.AddHostedService<TelemetryPruningService>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>(ApplicationConstants.DatabaseHealthCheckName);

        return services;
    }
}
