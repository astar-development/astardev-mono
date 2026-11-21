namespace AStarDev.CloudSync.Persistence;

/// <summary>The database could not be reached or prepared, for a reason other than corruption: a missing or unwritable folder, a locked file. Fatal for startup.</summary>
/// <param name="Message">The underlying reason, suitable for logs.</param>
public sealed record DatabaseUnavailable(string Message);

/// <summary>The integrity check passed but applying a migration failed, so the schema cannot be relied on. Fatal for startup.</summary>
/// <param name="Message">The underlying reason, suitable for logs.</param>
public sealed record MigrationFailed(string Message);

/// <summary>Closed set of failures that stop the database being prepared. Corruption is not one of them: it is reported through <see cref="IDatabaseState"/>.</summary>
public union DatabaseError(DatabaseUnavailable, MigrationFailed);
