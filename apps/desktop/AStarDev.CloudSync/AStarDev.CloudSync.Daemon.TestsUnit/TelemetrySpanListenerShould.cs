using System.Diagnostics;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class TelemetrySpanListenerShould : IDisposable
{
    private const string SourceName = "AStarDev.CloudSync.Tests";

    private readonly string telemetryDirectory = Directory.CreateTempSubdirectory("cloudsync-spans-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(telemetryDirectory, recursive: true);
    }

    private DaemonTestHost CreateHost(Action<IServiceCollection>? configureServices = null)
        => DaemonTestHost.Create(configureServices: configureServices, configuration: new Dictionary<string, string?> { ["Telemetry:Database:Path"] = Path.Combine(telemetryDirectory, "telemetry.db") });

    private static async Task StartAsync(DaemonTestHost host)
    {
        foreach (var service in host.Services.GetServices<IHostedService>().Where(service => service.GetType().Name is "TelemetryBatchWriter" or "TelemetrySpanListener")) await service.StartAsync(Token);
    }

    private static async Task<IReadOnlyList<SpanView>> WaitForSpansAsync(DaemonTestHost host, Func<TracesView, bool> ready)
    {
        var query = host.Services.GetRequiredService<TracesQuery>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        TracesView view;
        do
        {
            await Task.Delay(50, Token);
            view = (await query.HandleAsync(new GetTraces(), Token)).Value.ShouldBeOfType<Ok<TracesView>>().Value;
        }
        while (!ready(view) && DateTimeOffset.UtcNow < deadline);

        return view.Spans;
    }

    [Fact]
    public async Task write_spans_to_the_telemetry_store()
    {
        string traceId;
        await using (var first = CreateHost())
        {
            await StartAsync(first);
            using var source = new ActivitySource(SourceName);
            using (var request = source.StartActivity("ui.request"))
            {
                traceId = request!.TraceId.ToString();
                using var daemonSpan = source.StartActivity("daemon.handle");
                daemonSpan!.SetStatus(ActivityStatusCode.Error, "Could not read /home/ada/plan.txt");
            }

            await WaitForSpansAsync(first, view => view.Spans.Count(span => span.TraceId == traceId) == 2);
        }

        await using var restarted = CreateHost();
        var stored = (await restarted.Services.GetRequiredService<TracesQuery>().HandleAsync(new GetTraces(TraceId: traceId), Token)).Value.ShouldBeOfType<Ok<TracesView>>().Value.Spans;

        stored.Select(span => span.Name).Order().ShouldBe(["daemon.handle", "ui.request"]);
        stored.Select(span => span.TraceId).Distinct().ShouldBe([traceId]);
        var child = stored.Single(span => span.Name == "daemon.handle");
        child.ParentSpanId.ShouldBe(stored.Single(span => span.Name == "ui.request").SpanId);
        child.Failed.ShouldBeTrue();
        child.Detail.ShouldBe("Could not read <path>");
    }

    [Fact]
    public async Task drop_spans_without_blocking_when_the_store_fails()
    {
        await using var host = CreateHost(services => services.RemoveAll<ITelemetryStore>().AddSingleton<ITelemetryStore, FailingTelemetryStore>());
        var writer = host.Services.GetRequiredService<ITelemetryWriter>();
        await StartAsync(host);
        using var source = new ActivitySource(SourceName);

        var elapsed = Stopwatch.StartNew();
        for (var index = 0; index < 100; index++) source.StartActivity("work")!.Dispose();
        elapsed.Stop();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (writer.DroppedCount == 0 && DateTimeOffset.UtcNow < deadline) await Task.Delay(20, Token);

        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        writer.DroppedCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ignore_spans_of_other_activity_sources()
    {
        await using var host = CreateHost();
        await StartAsync(host);
        using var other = new ActivitySource("Microsoft.EntityFrameworkCore.Tests");

        var started = other.StartActivity("ef.query");

        started.ShouldBeNull();
    }
}
