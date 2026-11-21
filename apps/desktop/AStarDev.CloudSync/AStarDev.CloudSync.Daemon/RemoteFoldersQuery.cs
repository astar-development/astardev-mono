using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetRemoteFolders"/> by listing one folder through <see cref="ICloudProvider"/>.</summary>
internal sealed class RemoteFoldersQuery(ICloudProvider provider)
{
    /// <summary>Lists the requested folder and keeps its subfolders.</summary>
    /// <returns>The subfolders sorted by name, or <see cref="HandlerFailed"/> carrying the provider's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public Task<Result<RemoteFoldersView, IpcError>> HandleAsync(GetRemoteFolders query, CancellationToken cancellationToken)
        => ListAsync(new RemoteItemId(query.ParentId ?? ApplicationConstants.DriveRootItemId), cancellationToken);

    private async Task<Result<RemoteFoldersView, IpcError>> ListAsync(RemoteItemId parent, CancellationToken cancellationToken)
        => await provider.ListChildrenAsync(parent, cancellationToken) switch
        {
            Ok<IReadOnlyList<ProviderItem>> ok => new Ok<RemoteFoldersView>(new RemoteFoldersView(FoldersOf(ok.Value))),
            Fail<ProviderError> fail => new Fail<IpcError>(new HandlerFailed(ProviderErrorMessages.Describe(fail.Error)))
        };

    private static List<RemoteFolderView> FoldersOf(IReadOnlyList<ProviderItem> items)
        => items
            .Where(item => item.ItemType == ItemType.Folder)
            .Select(item => new RemoteFolderView(item.RemoteItemId.Value, item.Path.Value[(item.Path.Value.LastIndexOf('/') + 1)..]))
            .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
