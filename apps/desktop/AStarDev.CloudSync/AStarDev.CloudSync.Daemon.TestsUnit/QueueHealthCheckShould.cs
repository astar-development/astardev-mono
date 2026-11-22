using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class QueueHealthCheckShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task report_dead_letters_as_degraded()
    {
        await using var host = await HostWithJobAsync();
        await host.AddOperationAsync(await JobAsync(host), OperationStatus.DeadLetter, Token);

        var report = await CheckAsync(host);

        report.Status.ShouldBe(HealthStatus.Degraded);
        report.Entries["queue"].Description!.ShouldContain("1");
    }

    [Fact]
    public async Task be_healthy_when_nothing_is_dead_lettered()
    {
        await using var host = await HostWithJobAsync();
        await host.AddOperationAsync(await JobAsync(host), OperationStatus.Running, Token);

        var report = await CheckAsync(host);

        report.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task be_unhealthy_when_the_queue_cannot_be_read()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());

        var report = await CheckAsync(host);

        report.Entries["queue"].Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await HostWithJobAsync();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(registration => registration.Name == "queue", cancelled.Token));
    }

    private static async Task<DaemonTestHost> HostWithJobAsync()
    {
        var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.AddAccountAsync("id", AccountStatus.Connected, Token);

        return host;
    }

    private static async Task<SyncJob> JobAsync(DaemonTestHost host)
    {
        var accounts = (await host.Services.GetRequiredService<IAccountStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Account>>>().Value;

        return await host.AddJobAsync(accounts[0], "/tmp/cloudsync-health", Token);
    }

    private static Task<HealthReport> CheckAsync(DaemonTestHost host)
        => host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(registration => registration.Name == "queue", Token);
}
