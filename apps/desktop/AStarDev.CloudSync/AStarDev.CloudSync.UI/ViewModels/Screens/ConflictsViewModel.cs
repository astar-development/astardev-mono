using System.Collections.ObjectModel;
using AStarDev.CloudSync.UI.Conflicts;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Conflicts screen: the open conflicts, a detail panel for the focused one, bulk selection and the S21 resolution actions behind a confirmation step.
/// The actions are disabled placeholders until the daemon can resolve conflicts, and the conflicts are sample data until it reports real ones.
/// </summary>
internal sealed class ConflictsViewModel : ViewModelBase, IScreen
{
    private static readonly ConflictResolution[] Resolutions = [ConflictResolution.KeepLocal, ConflictResolution.KeepRemote, ConflictResolution.KeepBoth, ConflictResolution.Defer];

    private readonly ILocalisationService localisation;

    private readonly IConflictSource source;

    private ConflictRowViewModel? selectedConflict;

    private ConflictResolution? pendingResolution;

    /// <summary>Creates the screen over the sample conflicts. This is the constructor dependency injection uses.</summary>
    public ConflictsViewModel(ILocalisationService localisation) : this(localisation, new SampleConflictSource())
    {
    }

    /// <summary>Creates the screen over <paramref name="source"/>.</summary>
    internal ConflictsViewModel(ILocalisationService localisation, IConflictSource source)
    {
        this.localisation = localisation;
        this.source = source;
        Rows = new ObservableCollection<ConflictRowViewModel>(source.GetConflicts().Select(item => new ConflictRowViewModel(item, localisation)));
        foreach (var row in Rows) row.PropertyChanged += (_, args) => OnRowChanged(args.PropertyName);
        Actions = Resolutions.Select(resolution => new ConflictActionViewModel(resolution, localisation.Get($"Conflicts.Action.{resolution}"), false)).ToList();
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Conflicts;

    /// <inheritdoc/>
    public string Title => localisation.Get("Conflicts.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Conflicts.Glyph");

    /// <summary>The open conflicts, in the order the source gave them.</summary>
    public ObservableCollection<ConflictRowViewModel> Rows { get; }

    /// <summary>True when there are no conflicts, so the screen shows the "nothing needs you" text.</summary>
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>The four S21 resolution buttons: keep local, keep remote, keep both, defer.</summary>
    public IReadOnlyList<ConflictActionViewModel> Actions { get; }

    /// <summary>True when the conflicts are invented for display.</summary>
    public bool IsSampleData => source.IsSampleData;

    /// <summary>The localised statement that the conflicts shown are sample data.</summary>
    public string SampleDataNotice => localisation.Get("Conflicts.SampleDataNotice");

    /// <summary>Why the action buttons are disabled.</summary>
    public string ActionsUnavailableText => localisation.Get("Conflicts.ActionsUnavailable");

    /// <summary>The always-visible statement that resolving never destroys the other version at once.</summary>
    public string RecoveryText => localisation.Get("Conflicts.Recovery");

    /// <summary>The conflict whose detail panel is open, or <see langword="null"/> for none.</summary>
    public ConflictRowViewModel? SelectedConflict
    {
        get => selectedConflict;
        set => SetProperty(ref selectedConflict, value);
    }

    /// <summary>How many rows are ticked for a bulk action.</summary>
    public int SelectedCount => Rows.Count(row => row.IsSelected);

    /// <summary>True when at least one row is ticked.</summary>
    public bool HasSelection => SelectedCount > 0;

    /// <summary>The localised "n selected" text.</summary>
    public string SelectedCountText => localisation.Format("Conflicts.SelectedCount", ("count", SelectedCount));

    /// <summary>True while waiting for the user to confirm or cancel a requested resolution.</summary>
    public bool IsConfirming => pendingResolution is not null;

    /// <summary>The resolution awaiting confirmation, or <see langword="null"/> when none.</summary>
    public ConflictResolution? PendingResolution => pendingResolution;

    /// <summary>The question shown while <see cref="IsConfirming"/>, naming the action and how many conflicts it covers; empty otherwise.</summary>
    public string ConfirmationText => pendingResolution is { } resolution
        ? localisation.Format("Conflicts.Confirm", ("action", localisation.Get($"Conflicts.Action.{resolution}")), ("count", Targets().Count))
        : string.Empty;

    /// <summary>Focuses the conflict named by <paramref name="targetId"/>. A null or unknown target leaves the current focus unchanged.</summary>
    /// <param name="targetId">A conflict id, as given by a deep link.</param>
    public void OnNavigatedTo(string? targetId)
    {
        var row = Rows.FirstOrDefault(candidate => candidate.Item.Id == targetId);
        if (row is not null) SelectedConflict = row;
    }

    /// <summary>Ticks every row.</summary>
    public void SelectAll()
    {
        foreach (var row in Rows) row.IsSelected = true;
    }

    /// <summary>Unticks every row.</summary>
    public void ClearSelection()
    {
        foreach (var row in Rows) row.IsSelected = false;
    }

    /// <summary>
    /// Asks the user to confirm <paramref name="resolution"/> for the ticked rows, or for the focused conflict when none is ticked. Nothing is resolved here.
    /// Does nothing when there is no target.
    /// </summary>
    /// <param name="resolution">The resolution to confirm.</param>
    public void RequestResolution(ConflictResolution resolution)
    {
        if (Targets().Count == 0) return;

        pendingResolution = resolution;
        NotifyConfirmationChanged();
    }

    /// <summary>Drops the pending resolution without applying it. Safe to call when nothing is pending.</summary>
    public void CancelResolution()
    {
        pendingResolution = null;
        NotifyConfirmationChanged();
    }

    private List<ConflictRowViewModel> Targets()
    {
        var ticked = Rows.Where(row => row.IsSelected).ToList();

        return ticked.Count > 0 || selectedConflict is null ? ticked : [selectedConflict];
    }

    private void OnRowChanged(string? property)
    {
        if (property != nameof(ConflictRowViewModel.IsSelected)) return;

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCountText));
    }

    private void NotifyConfirmationChanged()
    {
        OnPropertyChanged(nameof(IsConfirming));
        OnPropertyChanged(nameof(PendingResolution));
        OnPropertyChanged(nameof(ConfirmationText));
    }
}
