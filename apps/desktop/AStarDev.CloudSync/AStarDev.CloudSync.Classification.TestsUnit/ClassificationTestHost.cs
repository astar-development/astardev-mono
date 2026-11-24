using AStarDev.CloudSync.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Classification.TestsUnit;

internal sealed class ClassificationTestHost(IHost host, string directory) : IAsyncDisposable
{
    public IServiceProvider Services => host.Services;

    public static async Task<ClassificationTestHost> CreateAsync(CancellationToken cancellationToken, Action<IServiceCollection>? configureServices = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"cloudsync-classification-{Guid.NewGuid():N}");
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[$"{DatabaseOptions.ConfigurationSection}:{nameof(DatabaseOptions.Path)}"] = Path.Combine(directory, "cloudsync.db");
        builder.Configuration[$"{TelemetryDatabaseOptions.ConfigurationSection}:{nameof(TelemetryDatabaseOptions.Path)}"] = Path.Combine(directory, "telemetry.db");
        builder.Services.AddCloudSyncPersistence().AddClassification();
        configureServices?.Invoke(builder.Services);
        var testHost = new ClassificationTestHost(builder.Build(), directory);
        await testHost.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(cancellationToken);

        return testHost;
    }

    public async ValueTask DisposeAsync()
    {
        if (host is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else host.Dispose();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
