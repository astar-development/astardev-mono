using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class TelemetryLoggerProviderShould : IDisposable
{
    private const string Category = "AStarDev.CloudSync.Tests";

    private readonly string telemetryDirectory = Directory.CreateTempSubdirectory("cloudsync-logs-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(telemetryDirectory, recursive: true);
    }

    private DaemonTestHost CreateHost(Action<IServiceCollection>? configureServices = null)
        => DaemonTestHost.Create(configureServices: configureServices, configuration: new Dictionary<string, string?> { ["Telemetry:Database:Path"] = Path.Combine(telemetryDirectory, "telemetry.db") });

    private static async Task StartWriterAsync(DaemonTestHost host)
    {
        var writer = host.Services.GetServices<IHostedService>().Single(service => service.GetType().Name == "TelemetryBatchWriter");
        await writer.StartAsync(Token);
    }

    private static async Task<IReadOnlyList<LogEntryView>> WaitForLogsAsync(DaemonTestHost host, Func<LogsView, bool> ready)
    {
        var query = host.Services.GetRequiredService<LogsQuery>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        LogsView view;
        do
        {
            await Task.Delay(50, Token);
            view = (await query.HandleAsync(new GetLogs(), Token)).Value.ShouldBeOfType<Ok<LogsView>>().Value;
        }
        while (!ready(view) && DateTimeOffset.UtcNow < deadline);

        return view.Entries;
    }

    [Fact]
    public async Task write_log_entries_to_the_telemetry_store()
    {
        await using (var first = CreateHost())
        {
            await StartWriterAsync(first);
            TelemetryTestLog.Queued(first.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Category), 3);
            await WaitForLogsAsync(first, view => view.Entries.Any(entry => entry.Source == Category));
        }

        await using var restarted = CreateHost();
        var afterRestart = (await restarted.Services.GetRequiredService<LogsQuery>().HandleAsync(new GetLogs(Text: "Queued 3 operations"), Token)).Value.ShouldBeOfType<Ok<LogsView>>().Value.Entries;

        var entry = afterRestart.ShouldHaveSingleItem();
        entry.Level.ShouldBe((int)LogLevel.Warning);
        entry.Source.ShouldBe(Category);
    }

    [Fact]
    public async Task drop_entries_without_blocking_when_the_store_fails()
    {
        await using var host = CreateHost(services => services.RemoveAll<ITelemetryStore>().AddSingleton<ITelemetryStore, FailingTelemetryStore>());
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Category);
        var writer = host.Services.GetRequiredService<ITelemetryWriter>();
        await StartWriterAsync(host);

        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        for (var index = 0; index < 100; index++) TelemetryTestLog.Failed(logger, index);
        elapsed.Stop();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (writer.DroppedCount == 0 && DateTimeOffset.UtcNow < deadline) await Task.Delay(20, Token);

        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        writer.DroppedCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task keep_personal_data_out_of_stored_entries()
    {
        await using var host = CreateHost();
        await StartWriterAsync(host);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Category);

        TelemetryTestLog.Synced(logger, "/home/ada/Documents/plan.txt", "ada@example.com");
        var stored = (await WaitForLogsAsync(host, view => view.Entries.Any(entry => entry.Source == Category))).Single(entry => entry.Source == Category);

        stored.Message.ShouldNotContain("ada");
        stored.Message.ShouldNotContain("/");
        stored.Message.ShouldNotContain("@");
        stored.Message.ShouldBe("Synced <path> for <email> at <path>");
    }
}
