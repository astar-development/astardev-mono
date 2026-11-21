using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 4: mark running operations failed and requeue eligible work. Real work arrives in later slices.</summary>
internal sealed class QueueRecoveryStage : IStartupStage
{
    /// <inheritdoc/>
    public string Name => ApplicationConstants.QueueRecoveryStageName;

    /// <inheritdoc/>
    public Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
        => Task.FromResult<Result<Unit, StartupError>>(new Ok<Unit>(default));
}
