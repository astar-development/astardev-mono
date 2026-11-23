using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class TelemetryDatabaseShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task keep_the_sync_database_unchanged_while_telemetry_is_written()
    {
        await using var host = DatabaseTestHost.Create();
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var before = await File.ReadAllBytesAsync(host.DatabasePath, Token);
        var entries = Enumerable.Range(0, 50).Select(index => new TelemetryEntry(TelemetryKind.Log, DateTimeOffset.UtcNow, 2, "Sync", $"message {index}")).ToList();

        var appended = await host.Services.GetRequiredService<ITelemetryStore>().AppendAsync(entries, Token);
        var telemetryPath = host.Services.GetRequiredService<IOptions<TelemetryDatabaseOptions>>().Value.Path;

        appended.Value.ShouldBeOfType<Ok<Unit>>();
        telemetryPath.ShouldNotBe(host.DatabasePath);
        File.Exists(telemetryPath).ShouldBeTrue();
        (await File.ReadAllBytesAsync(host.DatabasePath, Token)).ShouldBe(before);
    }

    [Fact]
    public async Task fail_without_touching_the_sync_database_when_telemetry_is_unavailable()
    {
        var unavailable = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "telemetry.db");
        await using var host = DatabaseTestHost.Create(services => services.Configure<TelemetryDatabaseOptions>(options => options.Path = unavailable));
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var before = await File.ReadAllBytesAsync(host.DatabasePath, Token);

        var appended = await host.Services.GetRequiredService<ITelemetryStore>().AppendAsync([new TelemetryEntry(TelemetryKind.Log, DateTimeOffset.UtcNow, 2, "Sync", "x")], Token);

        appended.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBeOfType<StorageFailed>();
        (await File.ReadAllBytesAsync(host.DatabasePath, Token)).ShouldBe(before);
        host.Services.GetRequiredService<IDatabaseState>().Mode.ShouldBe(DatabaseMode.Normal);
    }
}
