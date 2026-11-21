using AStarDev.CloudSync.Persistence;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon.Startup;

/// <summary>Stage 2: telemetry, persistence, IPC and providers are registered. No synchronisation begins. Prepares the database: integrity check, then migrations.</summary>
internal sealed class InfrastructureInitialisationStage(IDatabaseInitialiser databaseInitialiser) : IStartupStage
{
    /// <inheritdoc/>
    public string Name => ApplicationConstants.InfrastructureInitialisationStageName;

    /// <inheritdoc/>
    /// <remarks>
    /// A corrupt database is not a stage failure: the daemon starts in recovery mode (see <see cref="IDatabaseState"/>) so the problem can be reported and diagnosed.
    /// An unreachable database or a failed migration is a <see cref="StartupError"/>.
    /// </remarks>
    public async Task<Result<Unit, StartupError>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await databaseInitialiser.InitialiseAsync(cancellationToken);

        return result switch
        {
            Ok<Unit> ok => ok,
            Fail<DatabaseError> { Error: var error } => new Fail<StartupError>(new StartupError(Name, Describe(error))),
        };
    }

    private static string Describe(DatabaseError error)
        => error switch
        {
            DatabaseUnavailable unavailable => $"The database is unavailable: {unavailable.Message}",
            MigrationFailed failed => $"Applying database migrations failed: {failed.Message}"
        };
}
