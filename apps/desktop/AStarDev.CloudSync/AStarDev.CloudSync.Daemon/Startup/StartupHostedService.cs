using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Runs the startup sequence when the host starts. A stage failure is fatal: the host does not start.</summary>
internal sealed class StartupHostedService(StartupSequence sequence) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var result = await sequence.RunAsync(cancellationToken);

        // The IHostedService contract can only fail the host start by throwing, so this is the one place a failed Result becomes an exception.
        if (result is Fail<StartupError> fail) throw new StartupFailedException(fail.Error);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
