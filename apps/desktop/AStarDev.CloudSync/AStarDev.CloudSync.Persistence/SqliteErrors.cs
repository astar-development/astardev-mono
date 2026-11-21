using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Recognises the SQLite failures the stores turn into domain errors instead of storage failures.</summary>
internal static class SqliteErrors
{
    private const int SqliteConstraint = 19;

    /// <summary>Tells a rejected duplicate or dangling reference apart from a failing database.</summary>
    /// <param name="exception">The exception captured by <c>Try.RunAsync</c> around a save.</param>
    /// <returns><see langword="true"/> when SQLite refused the write because a key, unique index or foreign key would be violated.</returns>
    public static bool IsConstraintViolation(Exception exception)
        => exception is DbUpdateException { InnerException: SqliteException { SqliteErrorCode: SqliteConstraint } };
}
