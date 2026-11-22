using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 5: start the background work. Today that is the crawl of every enabled job, which continues from its checkpoints; the operation scheduler, workers and delta processing arrive in later slices.</summary>
internal sealed class BackgroundServicesStage(ISyncJobStore jobs, SyncRunner runner, OperationScheduler scheduler, IDatabaseState databaseState, ILogger<BackgroundServicesStage> logger) : IStartupStage
{
    /// <inheritdoc/>
    public string Name => ApplicationConstants.BackgroundServicesStageName;

    /// <inheritdoc/>
    /// <remarks>
    /// Returns without waiting for any crawl. A database in recovery mode is skipped, because its jobs cannot be trusted. A job that cannot be started is logged and the others still start;
    /// jobs that cannot be listed are a <see cref="StartupError"/>.
    /// </remarks>
    public async Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (databaseState.Mode == DatabaseMode.Recovery) return new Ok<Unit>(default);

        scheduler.Start();

        return await jobs.ListAsync(cancellationToken) switch
        {
            Ok<IReadOnlyList<SyncJob>> listed => await ResumeAllAsync(listed.Value, cancellationToken),
            Fail<StoreError> fail => new Fail<StartupError>(new StartupError(Name, StoreErrorMessages.Describe(fail.Error, "read", "sync job")))
        };
    }

    private async Task<Result<Unit, StartupError>> ResumeAllAsync(IReadOnlyList<SyncJob> listed, CancellationToken cancellationToken)
    {
        foreach (var job in listed.Where(job => job.Enabled))
        {
            if (await runner.StartAsync(job.Id, cancellationToken) is Fail<StoreError> fail) logger.LogWarning("Job {Job} could not be resumed: {Message}", job.Id.Value, StoreErrorMessages.Describe(fail.Error, "read", "sync job"));
        }

        return new Ok<Unit>(default);
    }
}
