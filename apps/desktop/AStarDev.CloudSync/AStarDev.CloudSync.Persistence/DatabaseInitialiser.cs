using AStarDev.FunctionalParadigm;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Startup stage 2 work for the database: integrity check, then migrations, or recovery mode when the file is corrupt.</summary>
internal sealed class DatabaseInitialiser(IOptions<DatabaseOptions> options, IDbContextFactory<CloudSyncDbContext> contextFactory, DatabaseState state, ILogger<DatabaseInitialiser> logger) : IDatabaseInitialiser
{
    private const int SqliteCorrupt = 11;
    private const int SqliteNotADatabase = 26;

    /// <inheritdoc/>
    public async Task<Result<Unit, DatabaseError>> InitialiseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var folder = Try.Run(() => Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Value.Path))!));
        if (folder.Value is Failure<DirectoryInfo> folderFailure) return new Fail<DatabaseError>(new DatabaseUnavailable(folderFailure.Exception.Message));

        var integrity = await Try.RunAsync(CheckIntegrityAsync, cancellationToken).ConfigureAwait(false);

        switch (integrity.Value)
        {
            case Failure<string?> failure when IsCorruption(failure.Exception):
                return EnterRecovery(failure.Exception.Message);
            case Failure<string?> failure:
                return new Fail<DatabaseError>(new DatabaseUnavailable(failure.Exception.Message));
            case Success<string?> { Value: { } problem }:
                return EnterRecovery(problem);
        }

        var migration = await Try.RunAsync(MigrateAsync, cancellationToken).ConfigureAwait(false);
        if (migration.Value is Failure<Unit> migrationFailure) return new Fail<DatabaseError>(new MigrationFailed(migrationFailure.Exception.Message));

        state.MarkNormal();
        logger.LogInformation("Database {Path} passed its integrity check and is up to date", options.Value.Path);

        return new Ok<Unit>(default);
    }

    private static bool IsCorruption(Exception exception)
        => exception is SqliteException { SqliteErrorCode: SqliteCorrupt or SqliteNotADatabase };

    private async Task<string?> CheckIntegrityAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        var findings = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) findings.Add(reader.GetString(0));

        return findings is ["ok"] ? null : string.Join("; ", findings);
    }

    private async Task<Unit> MigrateAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        return default;
    }

    private Result<Unit, DatabaseError> EnterRecovery(string reason)
    {
        state.EnterRecovery(reason);
        logger.LogCritical("Database {Path} failed its integrity check, entering recovery mode and leaving the file untouched: {Reason}", options.Value.Path, reason);

        return new Ok<Unit>(default);
    }
}
