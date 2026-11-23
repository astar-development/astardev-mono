using AStarDev.CloudSync.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class DatabaseTestHost : IAsyncDisposable
{
    private readonly Action<IServiceCollection>? configureServices;
    private IHost host;

    private DatabaseTestHost(IHost host, string databasePath, Action<IServiceCollection>? configureServices)
    {
        this.host = host;
        this.configureServices = configureServices;
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    public IServiceProvider Services => host.Services;

    public static DatabaseTestHost Create(Action<IServiceCollection>? configureServices = null)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cloudsync-integration-{Guid.NewGuid():N}", "cloudsync.db");

        return new DatabaseTestHost(Build(databasePath, configureServices), databasePath, configureServices);
    }

    public async Task RestartAsync()
    {
        await DisposeHostAsync();
        host = Build(DatabasePath, configureServices);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeHostAsync();
        var directory = Path.GetDirectoryName(DatabasePath)!;
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static IHost Build(string databasePath, Action<IServiceCollection>? configureServices)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[$"{DatabaseOptions.ConfigurationSection}:{nameof(DatabaseOptions.Path)}"] = databasePath;
        builder.Configuration[$"{TelemetryDatabaseOptions.ConfigurationSection}:{nameof(TelemetryDatabaseOptions.Path)}"] = Path.Combine(Path.GetDirectoryName(databasePath)!, "telemetry.db");
        builder.Logging.ClearProviders();
        builder.Services.AddCloudSyncPersistence();
        configureServices?.Invoke(builder.Services);

        return builder.Build();
    }

    private async Task DisposeHostAsync()
    {
        if (host is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else host.Dispose();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    }
}
