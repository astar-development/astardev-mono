using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 2: telemetry, persistence, IPC and providers are registered. No synchronisation begins. Real work arrives in later slices.</summary>
public sealed class InfrastructureInitialisationStage : IStartupStage
{
    public string Name => "Infrastructure initialisation";

    public Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
        => Task.FromResult<Result<Unit, StartupError>>(new Ok<Unit>(default));
}
