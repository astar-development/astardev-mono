namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 1: host started, configuration loaded, logging initialised. The generic host has done this before the sequence runs.</summary>
public sealed class ProcessStartStage : IStartupStage
{
    public string Name => "Process start";

    public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
