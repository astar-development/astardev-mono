using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

internal sealed class CommandCounter : IDbCommandInterceptor
{
    private int commands;

    public int Commands => Volatile.Read(ref commands);

    public void Reset() => Interlocked.Exchange(ref commands, 0);

    public ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref commands);

        return ValueTask.FromResult(result);
    }

    public ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref commands);

        return ValueTask.FromResult(result);
    }

    public ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref commands);

        return ValueTask.FromResult(result);
    }
}
