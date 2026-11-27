using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>One row of the folder tree: a remote folder, a loading placeholder or an error message.</summary>
public sealed partial class FolderNodeViewModel : ViewModelBase
{
    private readonly FolderTreeViewModel? owner;
    private readonly ObservableCollection<FolderNodeViewModel> level;
    private bool loaded;

    internal FolderNodeViewModel(FolderRowKind kind, string id, string text, FolderTreeViewModel? owner)
        : this(kind, id, text, owner, [])
    {
    }

    private FolderNodeViewModel(FolderRowKind kind, string id, string text, FolderTreeViewModel? owner, ObservableCollection<FolderNodeViewModel> level)
    {
        this.level = level;
        Kind = kind;
        Id = id;
        Text = text;
        this.owner = owner;
        if (kind == FolderRowKind.Folder) Children.Add(Loading());
    }

    /// <summary>Whether this row is a folder, a loading placeholder, an error message or the row that lists more folders.</summary>
    public FolderRowKind Kind { get; }

    /// <summary>The provider's id for the folder. For a more row, the id of the folder whose subfolders it lists, empty at the top of the drive. Empty for loading and error rows.</summary>
    public string Id { get; }

    /// <summary>The folder's name, or the text of a loading, error or more row.</summary>
    public string Text { get; }

    /// <summary>The rows below this one. A folder holds a single loading row until it has been expanded; loading and error rows have none.</summary>
    public ObservableCollection<FolderNodeViewModel> Children { get; } = [];

    /// <summary>Two-way bound to the tree item. Setting it to <see langword="true"/> lists the folder's subfolders the first time, and again after a failed listing.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>True for the row that lists more folders, so the view shows it as a button.</summary>
    public bool IsMore => Kind == FolderRowKind.More;

    /// <summary>True for a folder row of a tree that can be ticked, so the view shows a checkbox only where ticking does something.</summary>
    public bool CanSelect => Kind == FolderRowKind.Folder && owner?.Selection is not null;

    /// <summary>
    /// Two-way bound to the checkbox of a folder row. Reads and writes the shared selection, so a folder shows as ticked whenever the selection holds it, however the tree was rebuilt.
    /// Always <see langword="false"/> for loading and error rows, and for a tree without a selection.
    /// </summary>
    public bool IsSelected
    {
        get => owner?.Selection?.IsSelected(Id) ?? false;
        set
        {
            if (Kind != FolderRowKind.Folder || owner?.Selection is not { } selection || selection.IsSelected(Id) == value) return;

            selection.Select(Id, value);
            OnPropertyChanged();
        }
    }

    internal static FolderNodeViewModel Loading() => new(FolderRowKind.Loading, string.Empty, ApplicationConstants.LoadingFoldersText, null);

    internal static FolderNodeViewModel Failure(string message) => new(FolderRowKind.Error, string.Empty, message, null);

    internal static FolderNodeViewModel More(string parentId, FolderTreeViewModel owner, ObservableCollection<FolderNodeViewModel> level) => new(FolderRowKind.More, parentId, ApplicationConstants.MoreFoldersText, owner, level);

    /// <summary>
    /// Lists the folders inside this one and shows them as children, or shows an error row when listing fails. Does nothing for loading and error rows,
    /// and for a folder whose subfolders are already shown. Cancellation surfaces as <see cref="OperationCanceledException"/>. Also exposed to the view as <c>ExpandCommand</c>.
    /// </summary>
    [RelayCommand]
    public async Task ExpandAsync(CancellationToken cancellationToken)
    {
        if (Kind != FolderRowKind.Folder || loaded || owner is null) return;

        Replace([Loading()]);
        var rows = await owner.ListAsync(Id, 0, Children, cancellationToken);
        loaded = rows.All(row => row.Kind != FolderRowKind.Error);
        Replace(rows);
    }

    /// <summary>
    /// Lists the next page of the folders beside this more row and shows them in its place, followed by a new more row while folders remain. When listing fails, an error row shows above this row so it can be chosen again.
    /// Does nothing for any other row. Cancellation surfaces as <see cref="OperationCanceledException"/>. Also exposed to the view as <c>LoadMoreCommand</c>.
    /// </summary>
    [RelayCommand]
    public async Task LoadMoreAsync(CancellationToken cancellationToken)
    {
        if (Kind != FolderRowKind.More || owner is null) return;

        var rows = await owner.ListAsync(Id.Length == 0 ? null : Id, level.Count(row => row.Kind == FolderRowKind.Folder), level, cancellationToken);
        foreach (var stale in level.Where(row => row.Kind is FolderRowKind.Error or FolderRowKind.More).ToList()) level.Remove(stale);
        foreach (var row in rows) level.Add(row);
        if (rows.Any(row => row.Kind == FolderRowKind.Error)) level.Add(this);
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value) ExpandCommand.Execute(null);
    }

    private void Replace(IReadOnlyList<FolderNodeViewModel> rows)
    {
        Children.Clear();
        foreach (var row in rows) Children.Add(row);
    }
}
