using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 5: start the operation scheduler, workers, provider polling and delta processing. Real work arrives in later slices.</summary>
public sealed class BackgroundServicesStage : IStartupStage
{
    public string Name => "Background services";

    public Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
        => Task.FromResult<Result<Unit, StartupError>>(new Ok<Unit>(default));
}
