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

    private static async Task<string?> EnvironmentValue(DistributedApplicationExecutionContext executionContext, IResource resource, string name, CancellationToken cancellationToken)
    {
        var configuration = await ExecutionConfigurationBuilder
            .Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(executionContext, NullLogger.Instance, cancellationToken);

        return configuration.EnvironmentVariables.FirstOrDefault(variable => variable.Key == name).Value;
    }
}
