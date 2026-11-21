using AStarDev.CloudSync.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ProviderHealthCheckShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task be_healthy_when_every_account_is_connected()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.AddAccountAsync("one", AccountStatus.Connected, Token);

        var report = await CheckAsync(host);

        report.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task be_healthy_when_no_account_is_connected_yet()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);

        var report = await CheckAsync(host);

        report.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task be_degraded_and_name_the_account_that_needs_to_sign_in_again()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.AddAccountAsync("one", AccountStatus.ReauthenticationRequired, Token);

        var report = await CheckAsync(host);

        report.Status.ShouldBe(HealthStatus.Degraded);
        report.Entries["provider"].Description!.ShouldContain("one@example.com");
    }

    [Fact]
    public async Task be_unhealthy_when_the_accounts_cannot_be_read()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());

        var report = await CheckAsync(host);

        report.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    private static Task<HealthReport> CheckAsync(DaemonTestHost host)
        => host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(registration => registration.Name == "provider", Token);
}
