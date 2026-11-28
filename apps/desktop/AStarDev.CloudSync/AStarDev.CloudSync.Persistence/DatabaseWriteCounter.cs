using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AStarDev.CloudSync.Persistence;

/// <summary>Counts the writes committed through <see cref="CloudSyncDbContext"/>: a transaction when it commits, a write outside one when it runs, since it commits on its own.</summary>
/// <remarks>A command is a write unless its text starts with <c>SELECT</c> or <c>PRAGMA</c>, so a doubtful command counts and costs a poller one extra read rather than a missed change.</remarks>
internal sealed class DatabaseWriteCounter : IDatabaseWrites, IDbCommandInterceptor, IDbTransactionInterceptor
{
    private long version;

    /// <inheritdoc/>
    public long Version => Interlocked.Read(ref version);

    /// <inheritdoc/>
    public DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        CountAutocommit(command);

        return result;
    }

    /// <inheritdoc/>
    public ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(ReaderExecuted(command, eventData, result));

    /// <inheritdoc/>
    public int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        CountAutocommit(command);

        return result;
    }

    /// <inheritdoc/>
    public ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(NonQueryExecuted(command, eventData, result));

    /// <inheritdoc/>
    public object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        CountAutocommit(command);

        return result;
    }

    /// <inheritdoc/>
    public ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(ScalarExecuted(command, eventData, result));

    /// <inheritdoc/>
    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Interlocked.Increment(ref version);

    /// <inheritdoc/>
    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionCommitted(transaction, eventData);

        return Task.CompletedTask;
    }

    private void CountAutocommit(DbCommand command)
    {
        if (command.Transaction is null && IsWrite(command.CommandText)) Interlocked.Increment(ref version);
    }

    private static bool IsWrite(string sql)
    {
        var text = sql.TrimStart();

        return !text.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase);
    }
}
