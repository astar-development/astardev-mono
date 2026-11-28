using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal sealed class DaemonTestHost(IHost host, string socketPath, string databasePath) : IAsyncDisposable
{
    public string SocketPath => socketPath;

    public string DatabasePath => databasePath;

    public IServiceProvider Services => host.Services;

    public static DaemonTestHost Create(Action<string>? seedDatabase = null, Action<IServiceCollection>? configureServices = null, IReadOnlyDictionary<string, string?>? configuration = null)
    {
        var socketPath = Path.Combine(Path.GetTempPath(), $"cloudsync-daemon-{Guid.NewGuid():N}.sock");
        var databasePath = Path.Combine(Path.GetTempPath(), $"cloudsync-daemon-{Guid.NewGuid():N}", "cloudsync.db");
        seedDatabase?.Invoke(databasePath);
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration[$"{ApplicationConstants.IpcConfigurationSection}:{nameof(IpcOptions.SocketPath)}"] = socketPath;
        builder.Configuration[$"{DatabaseOptions.ConfigurationSection}:{nameof(DatabaseOptions.Path)}"] = databasePath;
        builder.Configuration[$"{TelemetryDatabaseOptions.ConfigurationSection}:{nameof(TelemetryDatabaseOptions.Path)}"] = Path.Combine(Path.GetDirectoryName(databasePath)!, "telemetry.db");
        foreach (var pair in configuration ?? new Dictionary<string, string?>()) builder.Configuration[pair.Key] = pair.Value;
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

    public async Task<SyncJob> AddJobAsync(Account account, string localRoot, CancellationToken cancellationToken, bool enabled = true)
    {
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", localRoot, new RemoteItemId("root"), BootstrapMode.Merge, enabled, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        await Services.GetRequiredService<ISyncJobStore>().AddAsync(job, cancellationToken);

        return job;
    }

    public async Task<Operation> AddOperationAsync(SyncJob job, OperationStatus status, CancellationToken cancellationToken)
    {
        var operation = new Operation(new OperationId(Guid.NewGuid()), job.Id, new ItemId(Guid.NewGuid()), OperationType.UploadFile, OperationPriority.Background, OperationStatus.Pending, 0, DateTimeOffset.UnixEpoch, null, DateTimeOffset.UnixEpoch);
        var store = Services.GetRequiredService<IOperationStore>();
        await store.EnqueueAsync(operation, cancellationToken);
        if (status == OperationStatus.Pending) return operation;

        await store.ClaimNextAsync(DateTimeOffset.UtcNow, 1, cancellationToken);
        if (status == OperationStatus.DeadLetter) await store.FailAsync(operation.Id, "forbidden", null, cancellationToken);
        if (status == OperationStatus.Completed) await store.CompleteAsync(operation.Id, cancellationToken);

        return operation;
    }

    public Task StartAsync(CancellationToken cancellationToken) => host.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => host.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (host is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else host.Dispose();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete($"{socketPath}{ApplicationConstants.InstanceNameSuffix}.lock");
        var directory = Path.GetDirectoryName(databasePath)!;
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
