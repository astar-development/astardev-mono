using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal sealed class CommandCounter : IDbCommandInterceptor
{
    private int commands;

    public int Commands => Volatile.Read(ref commands);

    public void Reset() => Interlocked.Exchange(ref commands, 0);

    public DbCommand CommandCreated(CommandEndEventData eventData, DbCommand result)
    {
        Interlocked.Increment(ref commands);

        return result;
    }
}
