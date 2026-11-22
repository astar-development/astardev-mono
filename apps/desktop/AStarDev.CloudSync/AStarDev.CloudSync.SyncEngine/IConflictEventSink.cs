using AStarDev.CloudSync.Domain;

namespace AStarDev.CloudSync.SyncEngine;

/// <summary>Where the sync engine announces conflict changes. The daemon implements it by publishing IPC events; the engine never knows about IPC.</summary>
public interface IConflictEventSink
{
    /// <summary>Announces a conflict that was just persisted and journaled.</summary>
    /// <param name="conflict">The new open conflict.</param>
    /// <param name="cancellationToken">Cancelling abandons the delivery and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>Completes when the announcement is delivered. A sink that cannot deliver drops it instead of failing the caller; the conflict is already stored.</returns>
    Task DetectedAsync(Conflict conflict, CancellationToken cancellationToken);

    /// <summary>Announces that a conflict was resolved.</summary>
    /// <param name="conflict">The conflict, with the status it now has.</param>
    /// <param name="resolution">What the user chose, for example <c>KeepLocal</c>.</param>
    /// <param name="resolvedUtc">When the resolution was applied.</param>
    /// <param name="cancellationToken">Cancelling abandons the delivery and surfaces as <see cref="OperationCanceledException"/>.</param>
    /// <returns>Completes when the announcement is delivered. A sink that cannot deliver drops it instead of failing the caller.</returns>
    Task ResolvedAsync(Conflict conflict, string resolution, DateTimeOffset resolvedUtc, CancellationToken cancellationToken);
}
