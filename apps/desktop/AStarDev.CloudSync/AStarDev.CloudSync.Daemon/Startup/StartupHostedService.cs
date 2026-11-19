using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Runs the startup sequence when the host starts. A stage failure is fatal: the host does not start.</summary>
public sealed class StartupHostedService(StartupSequence sequence) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => sequence.RunAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
