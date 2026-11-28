using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

internal sealed class PersistenceTestHost(IHost host, string databasePath) : IAsyncDisposable
{
    public string DatabasePath => databasePath;

    public IServiceProvider Services => host.Services;

    public static PersistenceTestHost Create(Action<IConfigurationManager>? configure = null, Action<IServiceCollection>? configureServices = null)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cloudsync-persistence-{Guid.NewGuid():N}", "cloudsync.db");
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration[$"{DatabaseOptions.ConfigurationSection}:{nameof(DatabaseOptions.Path)}"] = databasePath;
        builder.Configuration[$"{TelemetryDatabaseOptions.ConfigurationSection}:{nameof(TelemetryDatabaseOptions.Path)}"] = Path.Combine(Path.GetDirectoryName(databasePath)!, "telemetry.db");
        configure?.Invoke(builder.Configuration);
        builder.Services.AddCloudSyncPersistence();
        configureServices?.Invoke(builder.Services);

        return new PersistenceTestHost(builder.Build(), databasePath);
    }

    public static async Task<PersistenceTestHost> CreateInitialisedAsync(CancellationToken cancellationToken)
    {
        var testHost = Create();
        await testHost.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(cancellationToken);

        return testHost;
    }

    public async ValueTask DisposeAsync()
    {
        if (host is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else host.Dispose();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(databasePath)!;
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
