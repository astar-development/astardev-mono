using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class DatabaseHealthCheckShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task be_healthy_once_the_database_is_initialised()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);

        var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(Token);

        report.Entries["database"].Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task be_unhealthy_in_recovery_mode()
    {
        await using var host = PersistenceTestHost.Create();
        Directory.CreateDirectory(Path.GetDirectoryName(host.DatabasePath)!);
        await File.WriteAllTextAsync(host.DatabasePath, new string('x', 4096), Token);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);

        var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(Token);

        report.Entries["database"].Status.ShouldBe(HealthStatus.Unhealthy);
        report.Entries["database"].Description.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task be_unhealthy_before_the_database_is_initialised()
    {
        await using var host = PersistenceTestHost.Create();

        var report = await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(Token);

        report.Entries["database"].Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(cancelled.Token));
    }
}
