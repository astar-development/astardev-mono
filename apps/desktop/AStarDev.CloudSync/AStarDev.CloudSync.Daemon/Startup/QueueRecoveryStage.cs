using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 4: mark running operations failed and requeue eligible work. Real work arrives in later slices.</summary>
public sealed class QueueRecoveryStage : IStartupStage
{
    public string Name => "Queue recovery";

    public Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
        => Task.FromResult<Result<Unit, StartupError>>(new Ok<Unit>(default));
}
