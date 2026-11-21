using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Runs the startup sequence when the host starts. A stage failure is fatal: the host does not start.</summary>
internal sealed class StartupHostedService(StartupSequence sequence) : IHostedService
{
    /// <summary>Runs every stage before the host reports started.</summary>
    /// <exception cref="StartupFailedException">A stage failed; the host does not start.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var result = await sequence.RunAsync(cancellationToken);

        // The IHostedService contract can only fail the host start by throwing, so this is the one place a failed Result becomes an exception.
        if (result is Fail<StartupError> fail) throw new StartupFailedException(fail.Error);
    }

    /// <summary>Completes at once: startup holds nothing that needs stopping.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
