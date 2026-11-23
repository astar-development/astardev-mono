using System.Text.Json;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class DiagnosticsQueryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<DiagnosticsView> DiagnosticsAsync(DaemonTestHost host)
    {
        var result = await host.Services.GetRequiredService<DiagnosticsQuery>().HandleAsync(new GetDiagnostics(), Token);

        return result.Value.ShouldBeOfType<Ok<DiagnosticsView>>().Value;
    }

    [Fact]
    public async Task report_each_health_check()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.AddAccountAsync("one", AccountStatus.ReauthenticationRequired, Token);

        var diagnostics = await DiagnosticsAsync(host);

        diagnostics.State.ShouldBe(DaemonState.Running);
        diagnostics.Version.ShouldNotBeEmpty();
        diagnostics.Checks.Select(check => check.Name).ShouldBe(["database", "provider", "queue"]);
        diagnostics.Checks.Single(check => check.Name == "database").Status.ShouldBe(ProviderHealthState.Healthy);
        diagnostics.Checks.Single(check => check.Name == "provider").Status.ShouldBe(ProviderHealthState.Degraded);
        diagnostics.Checks.Single(check => check.Name == "queue").Status.ShouldBe(ProviderHealthState.Healthy);
    }

    [Fact]
    public async Task leave_paths_and_account_names_out()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var account = await host.AddAccountAsync("one", AccountStatus.ReauthenticationRequired, Token);
        await host.AddJobAsync(account, "/tmp/cloudsync-diagnostics", Token);

        var text = JsonSerializer.Serialize(await DiagnosticsAsync(host));

        text.ShouldNotContain("example.com");
        text.ShouldNotContain("one@");
        text.ShouldNotContain("/tmp/cloudsync-diagnostics");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<DiagnosticsQuery>().HandleAsync(new GetDiagnostics(), cancelled.Token));
    }
}
