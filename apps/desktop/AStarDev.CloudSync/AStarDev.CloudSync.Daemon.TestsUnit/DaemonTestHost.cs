using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal sealed class DaemonTestHost(IHost host, string socketPath, string databasePath) : IAsyncDisposable
{
    public string SocketPath => socketPath;

    public string DatabasePath => databasePath;

    public IServiceProvider Services => host.Services;

    public static DaemonTestHost Create(Action<string>? seedDatabase = null, Action<IServiceCollection>? configureServices = null)
    {
        var socketPath = Path.Combine(Path.GetTempPath(), $"cloudsync-daemon-{Guid.NewGuid():N}.sock");
        var databasePath = Path.Combine(Path.GetTempPath(), $"cloudsync-daemon-{Guid.NewGuid():N}", "cloudsync.db");
        seedDatabase?.Invoke(databasePath);
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[$"{ApplicationConstants.IpcConfigurationSection}:{nameof(IpcOptions.SocketPath)}"] = socketPath;
        builder.Configuration[$"{DatabaseOptions.ConfigurationSection}:{nameof(DatabaseOptions.Path)}"] = databasePath;
        builder.Services.AddCloudSyncDaemon();
        configureServices?.Invoke(builder.Services);

        return new DaemonTestHost(builder.Build(), socketPath, databasePath);
    }

    public static DaemonTestHost CreateWith(ICloudProvider provider)
        => Create(configureServices: services => services.RemoveAll<ICloudProvider>().AddSingleton(provider));

    public async Task InitialiseDatabaseAsync(CancellationToken cancellationToken)
        => await Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(cancellationToken);

    public async Task<Account> AddAccountAsync(string identifier, AccountStatus status, CancellationToken cancellationToken)
    {
        await InitialiseDatabaseAsync(cancellationToken);
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", $"{identifier}@example.com", identifier, DateTimeOffset.UnixEpoch, status);
        await Services.GetRequiredService<IAccountStore>().AddAsync(account, cancellationToken);

        return account;
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
