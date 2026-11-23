namespace AStarDev.CloudSync.Ports;

/// <summary>Hands telemetry to a background batching writer. Never blocks and never fails the caller, so telemetry cannot hold up sync.</summary>
public interface ITelemetryWriter
{
    /// <summary>How many entries have been dropped, because the queue was full or a batch could not be stored, since the process started.</summary>
    long DroppedCount { get; }

    /// <summary>Queues an entry for the next batch.</summary>
    /// <param name="entry">The entry to store.</param>
    /// <returns><see langword="false"/> when the queue was full and the entry was dropped and counted.</returns>
    bool TryWrite(TelemetryEntry entry);
}
