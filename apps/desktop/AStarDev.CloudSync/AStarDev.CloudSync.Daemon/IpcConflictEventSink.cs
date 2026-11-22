using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.CloudSync.SyncEngine;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Tells every subscribed UI about conflict changes with <see cref="ConflictDetected"/> and <see cref="ConflictResolved"/> events.</summary>
internal sealed class IpcConflictEventSink(IIpcServer server, ConflictViewFactory views, ILogger<IpcConflictEventSink> logger) : IConflictEventSink
{
    /// <inheritdoc/>
    /// <remarks>The event carries the same path and sides as <see cref="GetConflicts"/>. When they cannot be read the event carries the conflict alone, without path and sides. A failed publish is logged and dropped: the conflict is stored, so a UI that missed the event finds it with <see cref="GetConflicts"/>.</remarks>
    public async Task DetectedAsync(Conflict conflict, CancellationToken cancellationToken)
    {
        var view = await views.CreateAsync(conflict.SyncJobId, [conflict], cancellationToken) switch
        {
            Ok<IReadOnlyList<ConflictView>> ok => ok.Value[0],
            Fail<StoreError> => ConflictViewFactory.Bare(conflict)
        };

        await PublishAsync(new ConflictDetected(view), cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>A failed publish is logged and dropped.</remarks>
    public Task ResolvedAsync(Conflict conflict, string resolution, DateTimeOffset resolvedUtc, CancellationToken cancellationToken)
        => PublishAsync(new ConflictResolved(conflict.Id.Value, conflict.SyncJobId.Value, conflict.ItemId.Value, resolution, resolvedUtc), cancellationToken);

    private async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent
    {
        if (await server.PublishAsync(@event, cancellationToken) is Fail<IpcError> fail) logger.LogWarning("Publishing {Event} failed: {Error}", typeof(TEvent).Name, fail.Error.Value);
    }
}
