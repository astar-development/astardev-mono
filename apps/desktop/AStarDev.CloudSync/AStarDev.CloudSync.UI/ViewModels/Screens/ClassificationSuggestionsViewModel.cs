using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Suggestions tab of the Classifications screen: the words found in file paths that are not yet terms, most common first. The user promotes a word into a category or ignores it; the daemon never assigns one by itself.
/// The scan reads every file path, so it runs each time the tab is shown or the screen is opened on it, and whenever <see cref="RefreshCommand"/> runs.
/// <see cref="Changed"/> is raised after the daemon accepted a promotion or an ignore, so the screen can read the terms again.
/// </summary>
internal sealed class ClassificationSuggestionsViewModel : ViewModelBase
{
    private readonly ILocalisationService localisation;
    private readonly Func<IIpcClient> createClient;
    private CategoryRowViewModel? category;
    private bool isLoaded;
    private int total;
    private int version;
    private string? errorMessage;

    /// <summary>Creates the tab with no suggestions until the first load.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="createClient">Creates a fresh client per request; one client is one connection.</param>
    public ClassificationSuggestionsViewModel(ILocalisationService localisation, Func<IIpcClient> createClient)
    {
        this.localisation = localisation;
        this.createClient = createClient;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync);
        PromoteCommand = new AsyncRelayCommand<SuggestionRowViewModel>(PromoteAsync);
        IgnoreCommand = new AsyncRelayCommand<SuggestionRowViewModel>(IgnoreAsync);
    }

    /// <summary>Raised after the daemon accepted a promotion or an ignore.</summary>
    public event Action? Changed;

    /// <summary>The suggested words, most common first.</summary>
    public ObservableCollection<SuggestionRowViewModel> Rows { get; } = [];

    /// <summary>The category a promoted or ignored word goes into. The user must choose one before promoting.</summary>
    public CategoryRowViewModel? Category
    {
        get => category;
        set => SetProperty(ref category, value);
    }

    /// <summary>True once the scan has run, so the tab shows "no suggestions" only after it looked.</summary>
    public bool IsLoaded => isLoaded;

    /// <summary>True when the scan ran and found nothing.</summary>
    public bool IsEmpty => isLoaded && Rows.Count == 0;

    /// <summary>True when the daemon found more words than it listed, so the tab says it shows the most common ones.</summary>
    public bool IsCut => total > Rows.Count;

    /// <summary>How many words are shown out of how many were found.</summary>
    public string SummaryText => localisation.Format("Classifications.Suggestions.Summary", ("shown", Rows.Count), ("total", total));

    /// <summary>Why the last scan or change failed, worded for the user. <see langword="null"/> after one that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Scans the file paths again.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Reads the next page of less common words and adds it below those shown. The view offers it while <see cref="IsCut"/> is true.</summary>
    public IAsyncRelayCommand LoadMoreCommand { get; }

    /// <summary>Label of the button that loads less common words.</summary>
    public string MoreText => localisation.Get("Classifications.Suggestions.More");

    /// <summary>Makes the row's word an active term in <see cref="Category"/>.</summary>
    public IAsyncRelayCommand<SuggestionRowViewModel> PromoteCommand { get; }

    /// <summary>Marks the row's word ignored so it is never suggested again.</summary>
    public IAsyncRelayCommand<SuggestionRowViewModel> IgnoreCommand { get; }

    /// <summary>Label of the scan button.</summary>
    public string RefreshText => localisation.Get("Classifications.Suggestions.Refresh");

    /// <summary>Label of the promote button.</summary>
    public string PromoteText => localisation.Get("Classifications.Suggestions.Promote");

    /// <summary>Label of the ignore button.</summary>
    public string IgnoreText => localisation.Get("Classifications.Suggestions.Ignore");

    /// <summary>Text shown when the scan found nothing.</summary>
    public string EmptyText => localisation.Get("Classifications.Suggestions.Empty");

    /// <summary>Text above the category picker.</summary>
    public string CategoryText => localisation.Get("Classifications.Suggestions.Category");

    /// <summary>Reads the first page of suggestions from the daemon, replacing the words shown. A failure keeps the words shown and lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    public Task LoadAsync(CancellationToken cancellationToken) => LoadPageAsync(true, cancellationToken);

    /// <summary>Reads the page of less common words after those shown and adds it below them. A reply that a newer <see cref="LoadAsync"/> has replaced is dropped; a failure keeps the words shown and lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    public Task LoadMoreAsync(CancellationToken cancellationToken) => LoadPageAsync(false, cancellationToken);

    private async Task LoadPageAsync(bool restart, CancellationToken cancellationToken)
    {
        var asked = restart ? ++version : version;
        var skip = restart ? 0 : Rows.Count;
        await using var client = createClient();

        var result = await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await ScanAsync(client, skip, cancellationToken),
            Fail<ConnectFailure> fail => fail
        };
        if (asked == version) Show(result, restart);
    }

    private void Show(Result<ClassificationSuggestionsView, ConnectFailure> result, bool restart)
    {
        switch (result)
        {
            case Ok<ClassificationSuggestionsView> ok:
                if (restart) Rows.Clear();
                foreach (var suggestion in ok.Value.Suggestions) Rows.Add(Row(suggestion));

                isLoaded = true;
                total = ok.Value.Total;
                ErrorMessage = null;
                OnPropertyChanged(nameof(IsCut));
                OnPropertyChanged(nameof(SummaryText));
                OnPropertyChanged(nameof(IsLoaded));
                OnPropertyChanged(nameof(IsEmpty));
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private static async Task<Result<Unit, ConnectFailure>> ConnectAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<ClassificationSuggestionsView, ConnectFailure>> ScanAsync(IIpcClient client, int skip, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetClassificationSuggestions(skip), cancellationToken) switch
        {
            Ok<ClassificationSuggestionsView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private Task PromoteAsync(SuggestionRowViewModel? row, CancellationToken cancellationToken)
        => ApplyAsync(row, chosen => new PromoteSuggestion(row!.Word, chosen.Id), cancellationToken);

    private Task IgnoreAsync(SuggestionRowViewModel? row, CancellationToken cancellationToken)
        => ApplyAsync(row, chosen => new IgnoreSuggestion(row!.Word, chosen.Id), cancellationToken);

    private async Task ApplyAsync(SuggestionRowViewModel? row, Func<CategoryRowViewModel, ICommand> command, CancellationToken cancellationToken)
    {
        if (row is null) return;

        if (category is not { } chosen)
        {
            ErrorMessage = localisation.Get("Classifications.Suggestions.ChooseCategory");

            return;
        }

        switch (await ClassificationRequests.SendAsync(createClient, command(chosen), localisation.Get("Classifications.Editor.Refused"), cancellationToken))
        {
            case Ok<Unit>:
                if (Rows.Remove(row)) total--;

                ErrorMessage = null;
                OnPropertyChanged(nameof(IsCut));
                OnPropertyChanged(nameof(SummaryText));
                OnPropertyChanged(nameof(IsEmpty));
                Changed?.Invoke();
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private SuggestionRowViewModel Row(ClassificationSuggestionView suggestion)
        => new(suggestion.Word, localisation.Format(suggestion.IsFirstName ? "Classifications.Suggestion.DetailFirstName" : "Classifications.Suggestion.Detail", ("count", suggestion.FileCount)));
}
