using Aspire.Hosting;

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
        var daemonPath = await EnvironmentValue(daemon, "Ipc__SocketPath");
        var uiPath = await EnvironmentValue(ui, "Ipc__SocketPath");
        daemonPath.ShouldNotBeNullOrWhiteSpace();
        uiPath.ShouldBe(daemonPath);
    }

    static async Task<string?> EnvironmentValue(IResource resource, string name)
    {
        var values = await ((IResourceWithEnvironment)resource).GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Run);

        return values.GetValueOrDefault(name);
    }
}
