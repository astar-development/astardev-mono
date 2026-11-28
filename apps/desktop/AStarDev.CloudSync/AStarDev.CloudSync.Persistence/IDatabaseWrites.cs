namespace AStarDev.CloudSync.Persistence;

/// <summary>Tells a poller whether the state database may have changed since it last read, so it can skip reads while nothing is written. Safe to read from any thread.</summary>
public interface IDatabaseWrites
{
    /// <summary>Starts at 0 and grows after every write this process commits to the state database, so an equal value means nothing was committed in between. Writes by another process are not seen; the telemetry database is not counted.</summary>
    long Version { get; }
}
