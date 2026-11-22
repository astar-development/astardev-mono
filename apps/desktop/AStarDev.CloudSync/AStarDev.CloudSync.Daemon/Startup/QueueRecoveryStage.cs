using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 4: operations left running by a daemon that stopped mid-operation are marked with the reason and put back in the queue to run again.</summary>
internal sealed class QueueRecoveryStage(IOperationStore operations, IDatabaseState databaseState, ILogger<QueueRecoveryStage> logger) : IStartupStage
{
    /// <inheritdoc/>
    public string Name => ApplicationConstants.QueueRecoveryStageName;

    /// <inheritdoc/>
    /// <remarks>A database in recovery mode is skipped, because its queue cannot be trusted. Dead-lettered operations are left as they are. A queue that cannot be written is a <see cref="StartupError"/>.</remarks>
    public async Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (databaseState.Mode == DatabaseMode.Recovery) return new Ok<Unit>(default);

        var requeued = await operations.RequeueRunningAsync(ApplicationConstants.InterruptedOperationReason, cancellationToken);

        return requeued switch
        {
            Ok<int> ok => Log(ok.Value),
            Fail<StoreError> fail => new Fail<StartupError>(new StartupError(Name, StoreErrorMessages.Describe(fail.Error, "requeued", "interrupted operations")))
        };
    }

    private Result<Unit, StartupError> Log(int requeued)
    {
        if (requeued > 0) logger.LogWarning("{Count} operations were interrupted by the last shutdown and were requeued.", requeued);

        return new Ok<Unit>(default);
    }
}
