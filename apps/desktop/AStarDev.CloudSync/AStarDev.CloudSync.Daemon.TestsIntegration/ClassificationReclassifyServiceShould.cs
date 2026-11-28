using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.TestsIntegration;

public sealed class ClassificationReclassifyServiceShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task start_after_the_startup_sequence()
    {
        await using var host = DaemonTestHost.Create();

        var hostedServices = host.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();

        hostedServices.IndexOf(typeof(ClassificationReclassifyService)).ShouldBeGreaterThan(hostedServices.IndexOf(typeof(StartupHostedService)));
    }

    [Fact]
    public async Task record_a_run_once_the_seed_has_been_applied()
    {
        await using var host = DaemonTestHost.Create();
        await host.Services.GetServices<IStartupStage>().OfType<InfrastructureInitialisationStage>().Single().ExecuteAsync(Token);
        var service = host.Services.GetServices<IHostedService>().OfType<ClassificationReclassifyService>().Single();

        await service.StartAsync(Token);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30), Token);

        (await host.Services.GetRequiredService<IReclassifyRunStore>().GetLastCompletedAsync(Token)).Value.ShouldBeOfType<Ok<Option<ReclassifyRun>>>().Value.Value.ShouldBeOfType<Some<ReclassifyRun>>();
    }
}
