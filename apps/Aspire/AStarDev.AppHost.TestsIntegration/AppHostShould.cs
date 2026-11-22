using Aspire.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AStarDev.AppHost.TestsIntegration;

public sealed class AppHostShould
{
    [Fact]
    public async Task start_the_daemon_and_ui_resources()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AStarDev_AppHost>(cancellationToken);

        await using var app = await builder.BuildAsync(cancellationToken);

        var names = builder.Resources.Select(resource => resource.Name).ToList();
        names.ShouldContain("CloudSyncDaemon");
        names.ShouldContain("CloudSync");
    }

    [Fact]
    public async Task make_the_ui_wait_for_the_daemon()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AStarDev_AppHost>(cancellationToken);

        await using var app = await builder.BuildAsync(cancellationToken);

        var ui = builder.Resources.Single(resource => resource.Name == "CloudSync");
        ui.Annotations.OfType<WaitAnnotation>().Select(wait => wait.Resource.Name).ShouldContain("CloudSyncDaemon");
    }

    [Fact]
    public async Task give_the_daemon_and_ui_the_same_socket_path()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AStarDev_AppHost>(cancellationToken);

        await using var app = await builder.BuildAsync(cancellationToken);

        var daemon = builder.Resources.Single(resource => resource.Name == "CloudSyncDaemon");
        var ui = builder.Resources.Single(resource => resource.Name == "CloudSync");
        var daemonPath = await EnvironmentValue(builder.ExecutionContext, daemon, "Ipc__SocketPath", cancellationToken);
        var uiPath = await EnvironmentValue(builder.ExecutionContext, ui, "Ipc__SocketPath", cancellationToken);
        daemonPath.ShouldNotBeNullOrWhiteSpace();
        uiPath.ShouldBe(daemonPath);
    }

    [Fact]
    public async Task send_the_daemon_and_ui_telemetry_to_the_dashboard()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AStarDev_AppHost>(["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL=http://localhost:19213"], cancellationToken);

        await using var app = await builder.BuildAsync(cancellationToken);

        foreach (var name in new[] { "CloudSyncDaemon", "CloudSync" })
        {
            var resource = builder.Resources.Single(candidate => candidate.Name == name);
            (await EnvironmentValue(builder.ExecutionContext, resource, "OpenTelemetry__OtlpEndpoint", cancellationToken)).ShouldBe("http://localhost:19213");
        }
    }

    private static async Task<string?> EnvironmentValue(DistributedApplicationExecutionContext executionContext, IResource resource, string name, CancellationToken cancellationToken)
    {
        var configuration = await ExecutionConfigurationBuilder
            .Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(executionContext, NullLogger.Instance, cancellationToken);

        return configuration.EnvironmentVariables.FirstOrDefault(variable => variable.Key == name).Value;
    }
}
