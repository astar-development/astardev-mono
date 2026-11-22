using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>The sink used when the host registers none: announcements go nowhere.</summary>
internal sealed class NullConflictEventSink : IConflictEventSink
{
    /// <inheritdoc/>
    public Task DetectedAsync(Conflict conflict, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ResolvedAsync(Conflict conflict, string resolution, DateTimeOffset resolvedUtc, CancellationToken cancellationToken) => Task.CompletedTask;
}
