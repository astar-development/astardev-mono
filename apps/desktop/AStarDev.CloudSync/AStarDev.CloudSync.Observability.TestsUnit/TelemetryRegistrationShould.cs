using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AStarDev.CloudSync.Observability.TestsUnit;

public sealed class TelemetryRegistrationShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task register_the_cloudsync_sources_and_meters()
    {
        using var host = TelemetryTestHost.Create("http://localhost:4317");
        await host.StartAsync(Token);
        using var cloudSyncSource = new ActivitySource("AStarDev.CloudSync.Persistence");
        using var otherSource = new ActivitySource("Other.Library");
        using var cloudSyncMeter = new Meter("AStarDev.CloudSync.IPC");
        using var otherMeter = new Meter("Other.Library");
        var cloudSyncCounter = cloudSyncMeter.CreateCounter<long>("requests");
        var otherCounter = otherMeter.CreateCounter<long>("requests");

        cloudSyncSource.HasListeners().ShouldBeTrue();
        otherSource.HasListeners().ShouldBeFalse();
        cloudSyncCounter.Enabled.ShouldBeTrue();
        otherCounter.Enabled.ShouldBeFalse();
        await host.StopAsync(Token);
    }

    [Fact]
    public async Task describe_the_service_with_semantic_convention_resource_attributes()
    {
        using var host = TelemetryTestHost.Create();
        await host.StartAsync(Token);

        var attributes = host.Services.GetRequiredService<TracerProvider>().GetResource().Attributes.ToDictionary(pair => pair.Key, pair => pair.Value);
        await host.StopAsync(Token);

        attributes["service.name"].ShouldBe(TelemetryTestHost.ServiceName);
        attributes["service.version"].ToString().ShouldNotBeNullOrWhiteSpace();
        attributes["service.instance.id"].ToString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task start_when_no_otlp_endpoint_is_configured()
    {
        using var host = TelemetryTestHost.Create();

        await host.StartAsync(Token);
        host.Services.GetRequiredService<MeterProvider>().ShouldNotBeNull();
        await host.StopAsync(Token);
    }

    [Fact]
    public async Task start_when_an_otlp_endpoint_is_configured()
    {
        using var host = TelemetryTestHost.Create("http://localhost:4317");

        await host.StartAsync(Token);
        host.Services.GetRequiredService<TracerProvider>().ShouldNotBeNull();
        await host.StopAsync(Token);
    }

    [Fact]
    public async Task export_log_messages_with_their_placeholders_replaced()
    {
        using var host = TelemetryTestHost.Create();
        await host.StartAsync(Token);

        var options = host.Services.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>().Value;
        await host.StopAsync(Token);

        options.IncludeFormattedMessage.ShouldBeTrue();
    }
}
