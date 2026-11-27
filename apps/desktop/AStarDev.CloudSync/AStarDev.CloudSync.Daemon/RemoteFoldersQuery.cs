using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Answers <see cref="GetRemoteFolders"/> by listing one folder through <see cref="ICloudProvider"/>.</summary>
internal sealed class RemoteFoldersQuery(ICloudProvider provider, IAccountStore accounts)
{
    /// <summary>Lists the requested folder and keeps the requested page of its subfolders. The provider cannot page by name, so every child is read and the page is cut here.</summary>
    /// <returns>The page of subfolders sorted by name with how many there are in all; a negative <see cref="GetRemoteFolders.Skip"/> counts as zero and a <see cref="GetRemoteFolders.Take"/> outside 1 to <see cref="ApplicationConstants.ListLimit"/> is brought into that range. <see cref="HandlerFailed"/> carries the provider's failure worded for the user. Cancellation propagates as <see cref="OperationCanceledException"/>.</returns>
    public async Task<Result<RemoteFoldersView, IpcError>> HandleAsync(GetRemoteFolders query, CancellationToken cancellationToken)
        => await AccountIdentifiers.FindAsync(accounts, query.AccountId, cancellationToken) switch
        {
            Ok<string> account => await ListAsync(account.Value, query, cancellationToken),
            Fail<IpcError> fail => fail
        };

    private async Task<Result<RemoteFoldersView, IpcError>> ListAsync(string account, GetRemoteFolders query, CancellationToken cancellationToken)
        => await provider.ListChildrenAsync(account, new RemoteItemId(query.ParentId ?? ApplicationConstants.DriveRootItemId), cancellationToken) switch
        {
            Ok<IReadOnlyList<ProviderItem>> ok => new Ok<RemoteFoldersView>(PageOf(FoldersOf(ok.Value), query)),
            Fail<ProviderError> fail => new Fail<IpcError>(new HandlerFailed(ProviderErrorMessages.Describe(fail.Error)))
        };

    private static RemoteFoldersView PageOf(List<RemoteFolderView> folders, GetRemoteFolders query)
        => new(folders.Count, [.. folders.Skip(Paging.Skip(query.Skip)).Take(Paging.Take(query.Take))]);

    private static List<RemoteFolderView> FoldersOf(IReadOnlyList<ProviderItem> items)
        => items
            .Where(item => item.ItemType == ItemType.Folder)
            .Select(item => new RemoteFolderView(item.RemoteItemId.Value, item.Path.Value[(item.Path.Value.LastIndexOf('/') + 1)..]))
            .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(folder => folder.Id, StringComparer.Ordinal)
            .ToList();
}
