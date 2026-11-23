using System.Diagnostics.Metrics;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class TelemetryMetricSamplerShould : IDisposable
{
    private const string MeterName = "AStarDev.CloudSync.Tests";

    private readonly string telemetryDirectory = Directory.CreateTempSubdirectory("cloudsync-metrics-").FullName;

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
        foreach (var service in host.Services.GetServices<IHostedService>().Where(service => service.GetType().Name is "TelemetryBatchWriter" or "TelemetryMetricSampler")) await service.StartAsync(Token);
    }

    private static async Task<IReadOnlyList<MetricSampleView>> WaitForSamplesAsync(DaemonTestHost host, GetMetrics request, Func<IReadOnlyList<MetricSampleView>, bool> ready)
    {
        var query = host.Services.GetRequiredService<MetricsQuery>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        IReadOnlyList<MetricSampleView> samples;
        do
        {
            await Task.Delay(50, Token);
            samples = (await query.HandleAsync(request, Token)).Value.ShouldBeOfType<Ok<MetricsView>>().Value.Samples;
        }
        while (!ready(samples) && DateTimeOffset.UtcNow < deadline);

        return samples;
    }

    [Fact]
    public async Task sample_the_cloudsync_meters_into_the_store()
    {
        await using var host = CreateHost();
        using var meter = new Meter(MeterName);
        var depth = 3L;
        meter.CreateObservableGauge("cloudsync.tests.depth", () => depth, "{operation}");
        var completed = meter.CreateCounter<long>("cloudsync.tests.completed", "{operation}");
        var duration = meter.CreateHistogram<double>("cloudsync.tests.duration", "s");
        using var other = new Meter("Microsoft.EntityFrameworkCore.Tests");
        var ignored = other.CreateCounter<long>("cloudsync.tests.ignored");
        await StartAsync(host);
        completed.Add(2, new KeyValuePair<string, object?>("path", "/home/ada/plan.txt"));
        completed.Add(3);
        duration.Record(1);
        duration.Record(3);
        ignored.Add(1);

        host.Services.GetServices<IHostedService>().OfType<TelemetryMetricSampler>().Single().Sample();
        var samples = await WaitForSamplesAsync(host, new GetMetrics(), found => found.Count(sample => sample.Name.StartsWith("cloudsync.tests.", StringComparison.Ordinal)) >= 3);

        samples.Single(sample => sample.Name == "cloudsync.tests.depth").ShouldSatisfyAllConditions(sample => sample.Value.ShouldBe(3), sample => sample.Unit.ShouldBe("{operation}"));
        samples.Single(sample => sample.Name == "cloudsync.tests.completed").Value.ShouldBe(5);
        samples.Single(sample => sample.Name == "cloudsync.tests.duration").Value.ShouldBe(2);
        samples.ShouldNotContain(sample => sample.Name == "cloudsync.tests.ignored");
    }

    [Fact]
    public async Task keep_the_samples_across_a_restart()
    {
        using var meter = new Meter(MeterName);
        var completed = meter.CreateCounter<long>("cloudsync.tests.restart", "{operation}");
        await using (var first = CreateHost())
        {
            await StartAsync(first);
            completed.Add(4);
            first.Services.GetServices<IHostedService>().OfType<TelemetryMetricSampler>().Single().Sample();
            await WaitForSamplesAsync(first, new GetMetrics(Name: "cloudsync.tests.restart"), found => found.Count == 1);
        }

        await using var restarted = CreateHost();
        var stored = (await restarted.Services.GetRequiredService<MetricsQuery>().HandleAsync(new GetMetrics(Name: "cloudsync.tests.restart"), Token)).Value.ShouldBeOfType<Ok<MetricsView>>().Value.Samples;

        stored.Select(sample => sample.Value).ShouldBe([4]);
    }

    [Fact]
    public async Task drop_samples_without_blocking_when_the_store_fails()
    {
        await using var host = CreateHost(services => services.RemoveAll<ITelemetryStore>().AddSingleton<ITelemetryStore, FailingTelemetryStore>());
        var writer = host.Services.GetRequiredService<ITelemetryWriter>();
        using var meter = new Meter(MeterName);
        var completed = meter.CreateCounter<long>("cloudsync.tests.failing", "{operation}");
        await StartAsync(host);
        completed.Add(1);

        host.Services.GetServices<IHostedService>().OfType<TelemetryMetricSampler>().Single().Sample();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (writer.DroppedCount == 0 && DateTimeOffset.UtcNow < deadline) await Task.Delay(20, Token);

        writer.DroppedCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task not_start_when_cancelled()
    {
        await using var host = CreateHost();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var sampler = host.Services.GetServices<IHostedService>().OfType<TelemetryMetricSampler>().Single();

        await Should.ThrowAsync<OperationCanceledException>(() => sampler.StartAsync(cancelled.Token));
    }
}
