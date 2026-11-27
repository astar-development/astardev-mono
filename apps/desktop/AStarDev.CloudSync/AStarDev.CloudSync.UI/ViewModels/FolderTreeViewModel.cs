using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>The remote folder tree: the folders at the top of the drive, each listing its subfolders only when it is first expanded.</summary>
/// <param name="createClient">Creates a fresh client per request; one client is one connection.</param>
/// <param name="selection">Holds which folders are ticked and which account's drive the tree shows. When <see langword="null"/>, or while it has no account, the tree shows a connect-first row and cannot be ticked.</param>
public sealed partial class FolderTreeViewModel(Func<IIpcClient> createClient, ScopeSelectionViewModel? selection = null) : ViewModelBase
{
    private int refreshes;

    internal ScopeSelectionViewModel? Selection => selection;

    /// <summary>The rows at the top of the drive: folders, or a single loading or error row. Replaced as a whole by every refresh.</summary>
    public ObservableCollection<FolderNodeViewModel> Roots { get; } = [];

    /// <summary>
    /// Lists the folders at the top of the drive. Failures show as an error row; cancellation surfaces as <see cref="OperationCanceledException"/>.
    /// Exposed to the view as <c>RefreshCommand</c>, which has no cancellation of its own, so overlapping refreshes never cancel each other.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var generation = Interlocked.Increment(ref refreshes);
        Roots.Clear();
        Roots.Add(FolderNodeViewModel.Loading());
        var rows = await ListAsync(null, cancellationToken);
        if (generation != Volatile.Read(ref refreshes)) return;

        Roots.Clear();
        foreach (var row in rows) Roots.Add(row);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task Refresh() => RefreshAsync(CancellationToken.None);

    internal async Task<IReadOnlyList<FolderNodeViewModel>> ListAsync(string? parentId, CancellationToken cancellationToken)
    {
        if (selection?.AccountId is not { } accountId) return [FolderNodeViewModel.Failure(ApplicationConstants.ConnectAccountFirstText)];

        await using var client = createClient();

        return await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> => await QueryAsync(client, accountId, parentId, cancellationToken),
            Fail<IpcError> fail => [FolderNodeViewModel.Failure(ConnectFailure.From(fail.Error).Message)]
        };
    }

    private async Task<IReadOnlyList<FolderNodeViewModel>> QueryAsync(IIpcClient client, Guid accountId, string? parentId, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetRemoteFolders(accountId, parentId), cancellationToken) switch
        {
            Ok<RemoteFoldersView> ok => ok.Value.Folders.Select(folder => new FolderNodeViewModel(FolderRowKind.Folder, folder.Id, folder.Name, this)).ToList(),
            Fail<IpcError> fail => [FolderNodeViewModel.Failure(ConnectFailure.From(fail.Error).Message)]
        };
}
