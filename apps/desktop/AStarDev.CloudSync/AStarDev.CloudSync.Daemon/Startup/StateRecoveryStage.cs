namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 3: load provider state, sync jobs, pending operations and open conflicts. Real work arrives in later slices.</summary>
public sealed class StateRecoveryStage : IStartupStage
{
    public string Name => "State recovery";

    public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
