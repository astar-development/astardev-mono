using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Conflict Centre of docs 08 and 21: the conflicts that wait for the user, a detail panel for the focused one, bulk selection and the four resolutions behind a confirmation step.
/// The list follows <see cref="ConnectionViewModel.Conflicts"/>, so it updates live when the daemon detects or resolves a conflict.
/// </summary>
internal sealed class ConflictsViewModel : ViewModelBase, IScreen
{
    private static readonly ConflictChoice[] Choices = [ConflictChoice.KeepLocal, ConflictChoice.KeepRemote, ConflictChoice.KeepBoth, ConflictChoice.Defer];

    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;
    private readonly Func<IIpcClient> createClient;
    private readonly ConnectionViewModel connection;
    private readonly TimeProvider time;

    private ConflictRowViewModel? selectedConflict;
    private ConflictChoice? pendingChoice;
    private string? errorMessage;

    /// <summary>Creates the screen over the conflicts the connection holds and follows them from then on.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date, time and number.</param>
    /// <param name="createClient">Creates a fresh client per decision; one client is one connection.</param>
    /// <param name="connection">Holds the conflicts the daemon reported; the screen rebuilds its rows whenever they change.</param>
    /// <param name="time">The clock the age of a conflict is measured with.</param>
    public ConflictsViewModel(ILocalisationService localisation, ICultureService culture, Func<IIpcClient> createClient, ConnectionViewModel connection, TimeProvider time)
    {
        this.localisation = localisation;
        this.culture = culture;
        this.createClient = createClient;
        this.connection = connection;
        this.time = time;
        Actions = Choices.Select(choice => new ConflictActionViewModel(choice, localisation.Get($"Conflicts.Action.{choice}"))).ToList();
        ConfirmCommand = new AsyncRelayCommand(ConfirmAsync);
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync);
        connection.Conflicts.CollectionChanged += (_, _) => Rebuild();
        connection.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConnectionViewModel.ConflictsTotal)) OnPropertyChanged(nameof(CanLoadMore));
        };
        Rebuild();
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Conflicts;

    /// <inheritdoc/>
    public string Title => localisation.Get("Conflicts.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Conflicts.Glyph");

    /// <summary>The conflicts that wait for the user loaded so far, oldest first, deferred ones included.</summary>
    public ObservableCollection<ConflictRowViewModel> Rows { get; } = [];

    /// <summary>Reads the next page of each job's conflicts and adds them to those shown.</summary>
    public IAsyncRelayCommand LoadMoreCommand { get; }

    /// <summary>True while more conflicts wait than those shown, so the view offers <see cref="LoadMoreCommand"/>.</summary>
    public bool CanLoadMore => connection.Conflicts.Count < connection.ConflictsTotal;

    /// <summary>True when there are no conflicts, so the screen shows the "nothing needs you" text.</summary>
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>The four resolution buttons: keep local, keep remote, keep both, defer. Keep both is labelled as the recommended, safest choice.</summary>
    public IReadOnlyList<ConflictActionViewModel> Actions { get; }

    /// <summary>The always-visible statement that resolving never destroys the other version at once.</summary>
    public string RecoveryText => localisation.Get("Conflicts.Recovery");

    /// <summary>The conflict whose detail panel is open, or <see langword="null"/> for none.</summary>
    public ConflictRowViewModel? SelectedConflict
    {
        get => selectedConflict;
        set
        {
            if (SetProperty(ref selectedConflict, value)) UpdateActions();
        }
    }

    /// <summary>How many rows are ticked for a bulk action.</summary>
    public int SelectedCount => Rows.Count(row => row.IsSelected);

    /// <summary>True when at least one row is ticked.</summary>
    public bool HasSelection => SelectedCount > 0;

    /// <summary>The localised "n selected" text.</summary>
    public string SelectedCountText => localisation.Format("Conflicts.SelectedCount", ("count", SelectedCount));

    /// <summary>True while waiting for the user to confirm or cancel a requested resolution.</summary>
    public bool IsConfirming => pendingChoice is not null;

    /// <summary>The resolution awaiting confirmation, or <see langword="null"/> when none.</summary>
    public ConflictChoice? PendingChoice => pendingChoice;

    /// <summary>The question shown while <see cref="IsConfirming"/>, naming the action and how many conflicts it covers; empty otherwise.</summary>
    public string ConfirmationText => pendingChoice is { } choice
        ? localisation.Format("Conflicts.Confirm", ("action", localisation.Get($"Conflicts.Action.{choice}")), ("count", Targets().Count))
        : string.Empty;

    /// <summary>Applies the pending resolution; the Confirm button runs it.</summary>
    public IAsyncRelayCommand ConfirmCommand { get; }

    /// <summary>Why the last decision failed, worded for the user. <see langword="null"/> after a decision that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Focuses the conflict named by <paramref name="targetId"/>. A null, malformed or unknown target leaves the current focus unchanged.</summary>
    /// <param name="targetId">A conflict id as text, as given by a deep link.</param>
    public void OnNavigatedTo(string? targetId)
    {
        var row = Rows.FirstOrDefault(candidate => candidate.Id.ToString() == targetId);
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
    /// Asks the user to confirm <paramref name="choice"/> for the ticked rows, or for the focused conflict when none is ticked. Nothing is sent here, so a bulk action is never automatic.
    /// Does nothing when there is no target.
    /// </summary>
    /// <param name="choice">The decision to confirm.</param>
    public void RequestResolution(ConflictChoice choice)
    {
        if (Targets().Count == 0) return;

        pendingChoice = choice;
        NotifyConfirmationChanged();
    }

    /// <summary>Drops the pending resolution without applying it. Safe to call when nothing is pending.</summary>
    public void CancelResolution()
    {
        pendingChoice = null;
        NotifyConfirmationChanged();
    }

    /// <summary>
    /// Sends the pending resolution for the ticked rows, or the focused conflict, then shows the conflicts that remain, so a deferred conflict stays listed marked as deferred.
    /// A refusal, or a daemon that cannot be reached, lands in <see cref="ErrorMessage"/>; when the daemon resolved only some conflicts the list still shows what remains.
    /// Does nothing when nothing is pending. The pending request is dropped as soon as this starts, so confirming twice sends once.
    /// </summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>; the daemon may still finish the request.</param>
    public async Task ConfirmAsync(CancellationToken cancellationToken)
    {
        if (pendingChoice is not { } choice) return;

        var ids = Targets().Select(row => row.Id).ToList();
        CancelResolution();
        if (ids.Count == 0) return;

        await using var client = createClient();

        Show(await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await SendAndListAsync(client, new ResolveConflict(ids, choice), cancellationToken),
            Fail<ConnectFailure> fail => fail
        });
    }

    /// <summary>Reads the page after those shown of each job's conflicts and adds them to the list. A failure keeps the conflicts shown and lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task LoadMoreAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        switch (await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await ListMoreAsync(client, cancellationToken),
            Fail<ConnectFailure> fail => fail
        })
        {
            case Ok<IReadOnlyDictionary<Guid, ConflictsView>> ok:
                connection.AddConflicts(ok.Value);
                ErrorMessage = null;
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private async Task<Result<IReadOnlyDictionary<Guid, ConflictsView>, ConnectFailure>> ListMoreAsync(IIpcClient client, CancellationToken cancellationToken)
        => await ConflictListing.ListAsync(client, connection.LoadedConflicts, cancellationToken) switch
        {
            Ok<IReadOnlyDictionary<Guid, ConflictsView>> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<Unit, ConnectFailure>> ConnectAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<Decision, ConnectFailure>> SendAndListAsync(IIpcClient client, ICommand command, CancellationToken cancellationToken)
        => await client.SendAsync(command, cancellationToken) switch
        {
            Ok<CommandResult> sent => await ListAsync(client, sent.Value, cancellationToken),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<Decision, ConnectFailure>> ListAsync(IIpcClient client, CommandResult sent, CancellationToken cancellationToken)
        => await ConflictListing.ListAsync(client, cancellationToken) switch
        {
            Ok<IReadOnlyDictionary<Guid, ConflictsView>> ok => new Ok<Decision>(new Decision(ok.Value, sent.Outcome == CommandOutcome.Accepted ? null : sent.Message ?? ApplicationConstants.RequestRejectedText)),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private void Show(Result<Decision, ConnectFailure> result)
    {
        switch (result)
        {
            case Ok<Decision> ok:
                connection.ReplaceConflicts(ok.Value.Remaining);
                ClearSelection();
                ErrorMessage = ok.Value.Problem;
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private List<ConflictRowViewModel> Targets()
    {
        var ticked = Rows.Where(row => row.IsSelected).ToList();

        return ticked.Count > 0 || selectedConflict is null ? ticked : [selectedConflict];
    }

    private void Rebuild()
    {
        var ticked = Rows.Where(row => row.IsSelected).Select(row => row.Id).ToHashSet();
        var focused = selectedConflict?.Id;
        var now = time.GetUtcNow();
        Rows.Clear();

        foreach (var view in connection.Conflicts.OrderBy(conflict => conflict.DetectedUtc).ThenBy(conflict => conflict.ConflictId))
        {
            var row = new ConflictRowViewModel(view, localisation, culture, now) { IsSelected = ticked.Contains(view.ConflictId) };
            row.PropertyChanged += (_, args) => OnRowChanged(args.PropertyName);
            Rows.Add(row);
        }

        SelectedConflict = Rows.FirstOrDefault(row => row.Id == focused);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CanLoadMore));
        OnRowChanged(nameof(ConflictRowViewModel.IsSelected));
    }

    private void OnRowChanged(string? property)
    {
        if (property != nameof(ConflictRowViewModel.IsSelected)) return;

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCountText));
        UpdateActions();
    }

    private void UpdateActions()
    {
        var enabled = Targets().Count > 0;
        foreach (var action in Actions) action.IsEnabled = enabled;
    }

    private void NotifyConfirmationChanged()
    {
        OnPropertyChanged(nameof(IsConfirming));
        OnPropertyChanged(nameof(PendingChoice));
        OnPropertyChanged(nameof(ConfirmationText));
    }

    private sealed record Decision(IReadOnlyDictionary<Guid, ConflictsView> Remaining, string? Problem);
}
