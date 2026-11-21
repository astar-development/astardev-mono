using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 3: load provider state, sync jobs, pending operations and open conflicts. Real work arrives in later slices.</summary>
internal sealed class StateRecoveryStage : IStartupStage
{
    public string Name => ApplicationConstants.StateRecoveryStageName;

    public Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken) =>
        Task.FromResult<Result<Unit, StartupError>>(new Ok<Unit>(default));
}
