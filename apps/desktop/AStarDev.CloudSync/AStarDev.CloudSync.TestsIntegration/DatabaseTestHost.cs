using AStarDev.CloudSync.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class DatabaseTestHost : IAsyncDisposable
{
    private IHost host;

    private DatabaseTestHost(IHost host, string databasePath)
    {
        this.host = host;
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    public IServiceProvider Services => host.Services;

    public static DatabaseTestHost Create()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cloudsync-integration-{Guid.NewGuid():N}", "cloudsync.db");

        return new DatabaseTestHost(Build(databasePath), databasePath);
    }

    public async Task RestartAsync()
    {
        await DisposeHostAsync();
        host = Build(DatabasePath);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeHostAsync();
        var directory = Path.GetDirectoryName(DatabasePath)!;
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static IHost Build(string databasePath)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[$"{DatabaseOptions.ConfigurationSection}:{nameof(DatabaseOptions.Path)}"] = databasePath;
        builder.Services.AddCloudSyncPersistence();

        return builder.Build();
    }

    private async Task DisposeHostAsync()
    {
        if (host is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else host.Dispose();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    }
}
