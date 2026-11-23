using AStarDev.CloudSync.IPC;

namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>Turns what the daemon reports as queued into the operations the list shows.</summary>
internal static class QueueEntries
{
    /// <summary>Maps one daemon operation. A status other than <c>Running</c> shows as pending; a daemon type the screen has no name for shows as a queue run.</summary>
    /// <param name="view">The operation as the daemon reported it.</param>
    /// <returns>The operation with the daemon's id as its identifier and the time it was queued as its timestamp.</returns>
    public static OperationEntry ToEntry(QueuedOperationView view)
        => new(view.OperationId.ToString(), view.Provider, HistoryEntries.TypeOf(view.Type), view.Status == nameof(OperationStatus.Running) ? OperationStatus.Running : OperationStatus.Pending, view.QueuedUtc, view.Path, view.SizeBytes);
}
