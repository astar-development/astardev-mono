using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Prepares the database at startup: integrity check first, then migrations. Cancellation propagates as <see cref="OperationCanceledException"/>.</summary>
public interface IDatabaseInitialiser
{
    /// <summary>
    /// Creates the folder, checks the file's integrity and, only if it is sound, applies pending migrations. Safe to call twice.
    /// A corrupt file is not a failure of this call: <see cref="IDatabaseState.Mode"/> becomes <see cref="DatabaseMode.Recovery"/>, nothing is migrated and the file is not modified.
    /// </summary>
    /// <param name="cancellationToken">Cancelling abandons the work; a half-applied migration is rolled back by SQLite.</param>
    /// <returns>Success (check <see cref="IDatabaseState.Mode"/> to learn whether recovery mode was entered), <see cref="DatabaseUnavailable"/> or <see cref="MigrationFailed"/>.</returns>
    Task<Result<Unit, DatabaseError>> InitialiseAsync(CancellationToken cancellationToken);
}
