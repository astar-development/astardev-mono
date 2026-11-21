using AStarDev.CloudSync.Persistence;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal sealed class DaemonTestHost(IHost host, string socketPath, string databasePath) : IAsyncDisposable
{
    public string SocketPath => socketPath;

    public string DatabasePath => databasePath;

    public IServiceProvider Services => host.Services;

    public static DaemonTestHost Create(Action<string>? seedDatabase = null)
    {
        var socketPath = Path.Combine(Path.GetTempPath(), $"cloudsync-daemon-{Guid.NewGuid():N}.sock");
        var databasePath = Path.Combine(Path.GetTempPath(), $"cloudsync-daemon-{Guid.NewGuid():N}", "cloudsync.db");
        seedDatabase?.Invoke(databasePath);
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[$"{ApplicationConstants.IpcConfigurationSection}:{nameof(IpcOptions.SocketPath)}"] = socketPath;
        builder.Configuration[$"{DatabaseOptions.ConfigurationSection}:{nameof(DatabaseOptions.Path)}"] = databasePath;
        builder.Services.AddCloudSyncDaemon();

        return new DaemonTestHost(builder.Build(), socketPath, databasePath);
    }

    public Task StartAsync(CancellationToken cancellationToken) => host.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => host.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (host is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else host.Dispose();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(databasePath)!;
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
