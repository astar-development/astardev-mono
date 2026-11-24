using AStarDev.CloudSync.Classification;
using AStarDev.CloudSync.Persistence;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 2: telemetry, persistence, IPC and providers are registered. No synchronisation begins. Prepares the database: integrity check, then migrations, then the classification seed.</summary>
internal sealed class InfrastructureInitialisationStage(IDatabaseInitialiser databaseInitialiser, IDatabaseState databaseState, IClassificationStartup classification) : IStartupStage
{
    /// <inheritdoc/>
    public string Name => ApplicationConstants.InfrastructureInitialisationStageName;

    /// <inheritdoc/>
    /// <remarks>
    /// A corrupt database is not a stage failure: the daemon starts in recovery mode (see <see cref="IDatabaseState"/>) so the problem can be reported and diagnosed.
    /// An unreachable database or a failed migration is a <see cref="StartupError"/>. The classification seed runs only on a database in normal mode and never fails the stage: the daemon works without classification.
    /// </remarks>
    public async Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await databaseInitialiser.InitialiseAsync(cancellationToken);

        return result switch
        {
            Ok<Unit> => await SeedClassificationAsync(cancellationToken),
            Fail<DatabaseError> { Error: var error } => new Fail<StartupError>(new StartupError(Name, Describe(error))),
        };
    }

    private async Task<Result<Unit, StartupError>> SeedClassificationAsync(CancellationToken cancellationToken)
    {
        if (databaseState.Mode == DatabaseMode.Normal) await classification.RunAsync(cancellationToken);

        return new Ok<Unit>(default);
    }

    private static string Describe(DatabaseError error)
        => error switch
        {
            DatabaseUnavailable unavailable => $"The database is unavailable: {unavailable.Message}",
            MigrationFailed failed => $"Applying database migrations failed: {failed.Message}"
        };
}
