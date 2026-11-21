using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>One row of the folder tree: a remote folder, a loading placeholder or an error message.</summary>
public sealed partial class FolderNodeViewModel : ViewModelBase
{
    private readonly FolderTreeViewModel? owner;
    private bool loaded;

    internal FolderNodeViewModel(FolderRowKind kind, string id, string text, FolderTreeViewModel? owner)
    {
        Kind = kind;
        Id = id;
        Text = text;
        this.owner = owner;
        if (kind == FolderRowKind.Folder) Children.Add(Loading());
    }

    /// <summary>Whether this row is a folder, a loading placeholder or an error message.</summary>
    public FolderRowKind Kind { get; }

    /// <summary>The provider's id for the folder. Empty for loading and error rows.</summary>
    public string Id { get; }

    /// <summary>The folder's name, or the message of a loading or error row.</summary>
    public string Text { get; }

    /// <summary>The rows below this one. A folder holds a single loading row until it has been expanded; loading and error rows have none.</summary>
    public ObservableCollection<FolderNodeViewModel> Children { get; } = [];

    /// <summary>Two-way bound to the tree item. Setting it to <see langword="true"/> lists the folder's subfolders the first time, and again after a failed listing.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

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

    /// <summary>
    /// Lists the folders inside this one and shows them as children, or shows an error row when listing fails. Does nothing for loading and error rows,
    /// and for a folder whose subfolders are already shown. Cancellation surfaces as <see cref="OperationCanceledException"/>. Also exposed to the view as <c>ExpandCommand</c>.
    /// </summary>
    [RelayCommand]
    public async Task ExpandAsync(CancellationToken cancellationToken)
    {
        if (Kind != FolderRowKind.Folder || loaded || owner is null) return;

        Replace([Loading()]);
        var rows = await owner.ListAsync(Id, cancellationToken);
        loaded = rows.All(row => row.Kind == FolderRowKind.Folder);
        Replace(rows);
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
