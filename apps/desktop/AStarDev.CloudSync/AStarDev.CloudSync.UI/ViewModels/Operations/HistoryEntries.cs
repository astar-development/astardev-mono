using AStarDev.CloudSync.IPC;

namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>Turns what the daemon reports as history into the operations the list shows.</summary>
internal static class HistoryEntries
{
    /// <summary>Maps one daemon entry. A daemon type the screen has no name for shows as a queue run; an outcome other than <c>Completed</c> shows as failed.</summary>
    /// <param name="view">The entry as the daemon reported it.</param>
    /// <returns>The operation with the daemon's id as its identifier.</returns>
    public static OperationEntry ToEntry(HistoryEntryView view)
        => new(view.OperationId.ToString(), view.Provider, TypeOf(view.Type), view.Status == nameof(OperationStatus.Completed) ? OperationStatus.Completed : OperationStatus.Failed, view.Timestamp, view.Path, view.SizeBytes);

    private static OperationType TypeOf(string daemonType) => daemonType switch
    {
        "UploadFile" => OperationType.Upload,
        "DownloadFile" => OperationType.Download,
        "RenameRemote" or "RenameLocal" => OperationType.Rename,
        "DeleteRemote" or "DeleteLocal" => OperationType.Delete,
        _ => OperationType.QueueRun
    };
}
