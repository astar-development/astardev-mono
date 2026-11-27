using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class FolderTreeViewModelShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FolderTreeViewModel TreeOf(Func<IIpcClient> client) => new(client, new ScopeSelectionViewModel(client, new FakeFolderPicker(null)) { AccountId = FakeIpcClient.KnownAccount });

    [Fact]
    public async Task load_children_when_a_node_expands()
    {
        var client = new FakeIpcClient([], available: true) { FoldersReply = query => query.ParentId is null ? Folders(("docs", "Documents")) : Folders(("r", "Reports"), ("t", "Taxes")) };
        var tree = TreeOf(() => client);
        await tree.RefreshAsync(Token);
        var documents = tree.Roots[0];
        documents.Children.ShouldHaveSingleItem().Kind.ShouldBe(FolderRowKind.Loading);
        client.FolderQueries.Count.ShouldBe(1);

        await documents.ExpandAsync(Token);

        documents.Children.Select(row => (row.Text, row.Kind)).ShouldBe([("Reports", FolderRowKind.Folder), ("Taxes", FolderRowKind.Folder)]);
        client.FolderQueries[1].ParentId.ShouldBe("docs");
    }

    [Fact]
    public async Task let_overlapping_refreshes_both_finish_without_cancelling_each_other()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new FakeIpcClient([], available: true) { HoldQuery = gate, FoldersReply = _ => Folders(("a", "Alpha")) };
        var tree = TreeOf(() => client);

        var first = tree.RefreshCommand.ExecuteAsync(null);
        var second = tree.RefreshCommand.ExecuteAsync(null);
        gate.SetResult();
        await Task.WhenAll(first, second).WaitAsync(Token);

        tree.Roots.Select(row => row.Text).ShouldBe(["Alpha"]);
    }

    [Fact]
    public async Task list_the_top_of_the_drive_first()
    {
        var client = new FakeIpcClient([], available: true) { FoldersReply = query => Folders(("a", "Alpha")) };
        var tree = TreeOf(() => client);

        await tree.RefreshAsync(Token);

        client.FolderQueries.ShouldHaveSingleItem().ParentId.ShouldBeNull();
        tree.Roots.Select(row => row.Text).ShouldBe(["Alpha"]);
    }

    [Fact]
    public async Task show_an_error_row_when_listing_fails()
    {
        var client = new FakeIpcClient([], available: true) { FoldersReply = query => query.ParentId is null ? Folders(("docs", "Documents")) : new Fail<IpcError>(new HandlerFailed("The provider could not be reached.")) };
        var tree = TreeOf(() => client);
        await tree.RefreshAsync(Token);

        await tree.Roots[0].ExpandAsync(Token);

        var row = tree.Roots[0].Children.ShouldHaveSingleItem();
        row.Kind.ShouldBe(FolderRowKind.Error);
        row.Text.ShouldBe("The provider could not be reached.");
    }

    [Fact]
    public async Task show_an_error_row_at_the_top_when_the_daemon_is_not_running()
    {
        var tree = TreeOf(() => new FakeIpcClient([], available: false));

        await tree.RefreshAsync(Token);

        var row = tree.Roots.ShouldHaveSingleItem();
        row.Kind.ShouldBe(FolderRowKind.Error);
        row.Text.ShouldContain("daemon");
    }

    [Fact]
    public async Task list_a_node_again_after_a_failure_when_it_expands_again()
    {
        var calls = 0;
        var client = new FakeIpcClient([], available: true) { FoldersReply = query => query.ParentId is null ? Folders(("docs", "Documents")) : ++calls == 1 ? new Fail<IpcError>(new HandlerFailed("Try later.")) : Folders(("sub", "Sub")) };
        var tree = TreeOf(() => client);
        await tree.RefreshAsync(Token);
        await tree.Roots[0].ExpandAsync(Token);

        await tree.Roots[0].ExpandAsync(Token);

        tree.Roots[0].Children.ShouldHaveSingleItem().Kind.ShouldBe(FolderRowKind.Folder);
    }

    [Fact]
    public async Task not_list_a_node_twice_once_it_has_loaded()
    {
        var client = new FakeIpcClient([], available: true) { FoldersReply = query => query.ParentId is null ? Folders(("root-x", "RootX")) : Folders(("x", "X")) };
        var tree = TreeOf(() => client);
        await tree.RefreshAsync(Token);

        await tree.Roots[0].ExpandAsync(Token);
        await tree.Roots[0].ExpandAsync(Token);

        client.FolderQueries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task load_when_the_view_sets_is_expanded()
    {
        var client = new FakeIpcClient([], available: true) { FoldersReply = query => query.ParentId is null ? Folders(("root-x", "RootX")) : Folders(("x", "X")) };
        var tree = TreeOf(() => client);
        await tree.RefreshAsync(Token);

        tree.Roots[0].IsExpanded = true;
        await tree.Roots[0].ExpandCommand.ExecuteAsync(null);

        client.FolderQueries.Count.ShouldBe(2);
        tree.Roots[0].Children.ShouldHaveSingleItem().Kind.ShouldBe(FolderRowKind.Folder);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var tree = TreeOf(() => new FakeIpcClient([], available: true));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => tree.RefreshAsync(cancelled.Token));
    }

    [Fact]
    public async Task tick_a_folder_into_the_selection()
    {
        var client = new FakeIpcClient([], available: true) { FoldersReply = _ => Folders(("docs", "Documents")) };
        var selection = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null)) { AccountId = FakeIpcClient.KnownAccount };
        var tree = new FolderTreeViewModel(() => client, selection);
        await tree.RefreshAsync(Token);

        tree.Roots[0].IsSelected = true;

        selection.SelectedFolderIds.ShouldBe(["docs"]);
        tree.Roots[0].IsSelected.ShouldBeTrue();
    }

    [Fact]
    public async Task show_a_stored_selection_as_ticked()
    {
        var client = new FakeIpcClient([], available: true) { FoldersReply = _ => Folders(("docs", "Documents"), ("notes", "Notes")) };
        var selection = new ScopeSelectionViewModel(() => client, new FakeFolderPicker(null)) { AccountId = FakeIpcClient.KnownAccount };
        selection.Select("notes", true);
        var tree = new FolderTreeViewModel(() => client, selection);

        await tree.RefreshAsync(Token);

        tree.Roots.Select(row => row.IsSelected).ShouldBe([false, true]);
    }

    private static Ok<RemoteFoldersView> Folders(params (string Id, string Name)[] folders)
        => new(new RemoteFoldersView(folders.Select(folder => new RemoteFolderView(folder.Id, folder.Name)).ToList()));
}
