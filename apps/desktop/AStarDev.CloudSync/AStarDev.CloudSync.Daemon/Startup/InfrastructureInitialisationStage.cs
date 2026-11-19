namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 2: telemetry, persistence, IPC and providers are registered. No synchronisation begins. Real work arrives in later slices.</summary>
public sealed class InfrastructureInitialisationStage : IStartupStage
{
    public string Name => "Infrastructure initialisation";

    public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
