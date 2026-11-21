using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>The remote folder tree: the folders at the top of the drive, each listing its subfolders only when it is first expanded.</summary>
/// <param name="createClient">Creates a fresh client per request; one client is one connection.</param>
public sealed partial class FolderTreeViewModel(Func<IIpcClient> createClient) : ViewModelBase
{
    /// <summary>The rows at the top of the drive: folders, or a single loading or error row. Replaced as a whole by every refresh.</summary>
    public ObservableCollection<FolderNodeViewModel> Roots { get; } = [];

    /// <summary>
    /// Lists the folders at the top of the drive. Failures show as an error row; cancellation surfaces as <see cref="OperationCanceledException"/>.
    /// Also exposed to the view as <c>RefreshCommand</c>.
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        Roots.Clear();
        Roots.Add(FolderNodeViewModel.Loading());
        var rows = await ListAsync(null, cancellationToken);
        Roots.Clear();
        foreach (var row in rows) Roots.Add(row);
    }

    internal async Task<IReadOnlyList<FolderNodeViewModel>> ListAsync(string? parentId, CancellationToken cancellationToken)
    {
        await using var client = createClient();

        return await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> => await QueryAsync(client, parentId, cancellationToken),
            Fail<IpcError> fail => [FolderNodeViewModel.Failure(ConnectFailure.From(fail.Error).Message)]
        };
    }

    private async Task<IReadOnlyList<FolderNodeViewModel>> QueryAsync(IIpcClient client, string? parentId, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetRemoteFolders(parentId), cancellationToken) switch
        {
            Ok<RemoteFoldersView> ok => ok.Value.Folders.Select(folder => new FolderNodeViewModel(FolderRowKind.Folder, folder.Id, folder.Name, this)).ToList(),
            Fail<IpcError> fail => [FolderNodeViewModel.Failure(ConnectFailure.From(fail.Error).Message)]
        };
}
