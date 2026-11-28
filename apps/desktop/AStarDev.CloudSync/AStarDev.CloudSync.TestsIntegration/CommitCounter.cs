using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class CommitCounter : IDbCommandInterceptor, IDbTransactionInterceptor
{
    private int commits;

    public int Commits => Volatile.Read(ref commits);

    public void Reset() => Interlocked.Exchange(ref commits, 0);

    public ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        CountAutocommit(command);

        return ValueTask.FromResult(result);
    }

    public ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        CountAutocommit(command);

        return ValueTask.FromResult(result);
    }

    public ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        CountAutocommit(command);

        return ValueTask.FromResult(result);
    }

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref commits);

        return Task.CompletedTask;
    }

    private void CountAutocommit(DbCommand command)
    {
        if (command.Transaction is null && IsWrite(command.CommandText)) Interlocked.Increment(ref commits);
    }

    private static bool IsWrite(string sql)
    {
        var text = sql.TrimStart();

        return !text.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase);
    }
}
