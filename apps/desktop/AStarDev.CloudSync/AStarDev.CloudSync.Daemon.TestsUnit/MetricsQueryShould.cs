using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class MetricsQueryShould
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static TelemetryEntry Sample(DateTimeOffset at, string name, string value, string unit = "{operation}") => new(TelemetryKind.Metric, at, 0, name, $"{value}\t{unit}");

    [Fact]
    public async Task return_recent_samples_for_an_instrument()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);
        await host.Services.GetRequiredService<ITelemetryStore>().AppendAsync(
            [
                Sample(Noon, "cloudsync.queue.pending", "4"),
                Sample(Noon.AddMinutes(1), "cloudsync.queue.pending", "2.5"),
                Sample(Noon.AddMinutes(1), "cloudsync.queue.running", "1"),
                new TelemetryEntry(TelemetryKind.Log, Noon, 2, "Sync", "not a sample")
            ],
            Token);
        var query = host.Services.GetRequiredService<MetricsQuery>();

        var one = await query.HandleAsync(new GetMetrics(Name: "cloudsync.queue.pending"), Token);
        var all = await query.HandleAsync(new GetMetrics(), Token);
        var recent = await query.HandleAsync(new GetMetrics(Since: Noon.AddMinutes(1)), Token);

        one.Value.ShouldBeOfType<Ok<MetricsView>>().Value.Samples.ShouldBe(
            [
                new MetricSampleView("cloudsync.queue.pending", Noon.AddMinutes(1), 2.5, "{operation}"),
                new MetricSampleView("cloudsync.queue.pending", Noon, 4, "{operation}")
            ]);
        all.Value.ShouldBeOfType<Ok<MetricsView>>().Value.Samples.Count.ShouldBe(3);
        recent.Value.ShouldBeOfType<Ok<MetricsView>>().Value.Samples.Select(sample => sample.Name).Order().ShouldBe(["cloudsync.queue.pending", "cloudsync.queue.running"]);
    }

    [Fact]
    public async Task leave_out_a_sample_that_is_not_in_the_stored_shape()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);
        await host.Services.GetRequiredService<ITelemetryStore>().AppendAsync([new TelemetryEntry(TelemetryKind.Metric, Noon, 0, "broken", "not a number")], Token);

        var result = await host.Services.GetRequiredService<MetricsQuery>().HandleAsync(new GetMetrics(), Token);

        result.Value.ShouldBeOfType<Ok<MetricsView>>().Value.Samples.ShouldBeEmpty();
    }

    [Fact]
    public async Task fail_with_a_message_when_the_store_fails()
    {
        await using var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ITelemetryStore>().AddSingleton<ITelemetryStore, FailingTelemetryStore>());

        var result = await host.Services.GetRequiredService<MetricsQuery>().HandleAsync(new GetMetrics(), Token);

        result.Value.ShouldBeOfType<Fail<IpcError>>().Error.Value.ShouldBeOfType<HandlerFailed>().Message.ShouldContain("metrics could not be read");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<MetricsQuery>().HandleAsync(new GetMetrics(), cancelled.Token));
    }
}
