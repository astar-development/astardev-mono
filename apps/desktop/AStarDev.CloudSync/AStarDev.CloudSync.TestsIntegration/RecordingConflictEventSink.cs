using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.SyncEngine;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class RecordingConflictEventSink : IConflictEventSink
{
    public List<Conflict> Detected { get; } = [];

    public Task DetectedAsync(Conflict conflict, CancellationToken cancellationToken)
    {
        lock (Detected) Detected.Add(conflict);

        return Task.CompletedTask;
    }

    public Task ResolvedAsync(Conflict conflict, string resolution, DateTimeOffset resolvedUtc, CancellationToken cancellationToken) => Task.CompletedTask;
}
