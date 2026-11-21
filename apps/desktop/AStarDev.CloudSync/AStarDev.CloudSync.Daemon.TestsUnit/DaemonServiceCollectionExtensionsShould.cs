using System.Diagnostics;
using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Persistence;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class DaemonServiceCollectionExtensionsShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task register_the_startup_stages_in_the_documented_order()
    {
        await using var host = DaemonTestHost.Create();

        host.Services.GetServices<IStartupStage>().Select(stage => stage.Name).ShouldBe(
        [
            "Process start",
            "Infrastructure initialisation",
            "State recovery",
            "Queue recovery",
            "Background services",
            "Accept connections"
        ]);
    }

    [Fact]
    public async Task start_the_ipc_server_only_after_the_startup_sequence()
    {
        await using var host = DaemonTestHost.Create();

        var hostedServices = host.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();

        hostedServices.IndexOf(typeof(StartupHostedService)).ShouldBeGreaterThanOrEqualTo(0);
        hostedServices.IndexOf(typeof(IpcHostedService)).ShouldBeGreaterThan(hostedServices.IndexOf(typeof(StartupHostedService)));
    }

    [Fact]
    public async Task answer_get_daemon_status_on_the_configured_socket_once_started()
    {
        await using var host = DaemonTestHost.Create();
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);

        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var status = (await client.QueryAsync(new GetDaemonStatus(), Token)).Value.ShouldBeOfType<Ok<DaemonStatusView>>().Value;
        await host.StopAsync(Token);

        status.State.ShouldBe(DaemonState.Running);
        status.Version.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task log_the_status_query_inside_the_trace_that_the_client_started()
    {
        var logs = new TraceCapturingLoggerProvider();
        await using var host = DaemonTestHost.Create(configureServices: services => services.AddSingleton<ILoggerProvider>(logs));
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        using var testSource = new ActivitySource(Guid.NewGuid().ToString("N"));
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        using var testRoot = testSource.StartActivity("test");
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        (await client.QueryAsync(new GetDaemonStatus(), Token)).Value.ShouldBeOfType<Ok<DaemonStatusView>>();
        await host.StopAsync(Token);

        logs.Entries.ShouldContain(entry => entry.Category == typeof(IpcHostedService).FullName && entry.Message.Contains("GetDaemonStatus") && entry.TraceId == testRoot!.TraceId);
    }

    [Fact]
    public async Task stop_listening_when_the_host_stops()
    {
        await using var host = DaemonTestHost.Create();
        await using var connected = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await connected.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        await host.StopAsync(Token);
        await connected.Disconnected.WaitAsync(Token);
        await using var late = UnixSocketIpc.CreateClient(host.SocketPath);

        (await late.ConnectAsync(Token)).Value.ShouldBeOfType<Fail<IpcError>>();
    }

    [Fact]
    public async Task export_logs_metrics_and_traces_as_the_daemon_service()
    {
        await using var host = DaemonTestHost.Create();
        await host.StartAsync(Token);
        using var source = new ActivitySource("AStarDev.CloudSync.Daemon");

        var serviceName = host.Services.GetRequiredService<TracerProvider>().GetResource().Attributes.ToDictionary(pair => pair.Key, pair => pair.Value)["service.name"];
        var loggerProviders = host.Services.GetServices<ILoggerProvider>().Select(provider => provider.GetType()).ToList();
        await host.StopAsync(Token);

        serviceName.ShouldBe("astardev-cloudsync-daemon");
        source.HasListeners().ShouldBeTrue();
        loggerProviders.ShouldContain(typeof(OpenTelemetryLoggerProvider));
    }

    [Fact]
    public async Task create_the_database_schema_when_the_host_starts()
    {
        await using var host = DaemonTestHost.Create();

        await host.StartAsync(Token);
        var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(Token);
        await host.StopAsync(Token);

        host.Services.GetRequiredService<IDatabaseState>().Mode.ShouldBe(DatabaseMode.Normal);
        File.Exists(host.DatabasePath).ShouldBeTrue();
        report.Entries["database"].Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task keep_serving_status_in_recovery_mode_and_report_the_database_unhealthy()
    {
        await using var host = DaemonTestHost.Create(InfrastructureInitialisationStageShould.CorruptDatabaseAt);
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);

        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var status = await client.QueryAsync(new GetDaemonStatus(), Token);
        var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(Token);
        await host.StopAsync(Token);

        status.Value.ShouldBeOfType<Ok<DaemonStatusView>>();
        host.Services.GetRequiredService<IDatabaseState>().Mode.ShouldBe(DatabaseMode.Recovery);
        report.Entries["database"].Status.ShouldBe(HealthStatus.Unhealthy);
    }
}
