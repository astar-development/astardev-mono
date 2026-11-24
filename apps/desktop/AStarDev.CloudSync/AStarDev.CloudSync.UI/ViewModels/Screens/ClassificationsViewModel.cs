using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Classifications screen: every term with its category, where it came from and how many files it has, filtered by text and ordered by term, category or file count.
/// Selecting a term lists its files a page at a time, each with the account that holds it. The daemon does the counting and the paging; the screen only shows what it is sent.
/// Terms load each time the screen is opened and whenever <see cref="RefreshCommand"/> runs.
/// Beside the terms it hosts the Suggestions tab, the forms that add, edit and remove categories and terms, and the reclassify control; a change any of them makes reads the terms again.
/// </summary>
internal sealed class ClassificationsViewModel : ViewModelBase, IScreen
{
    private const int FilePageSize = 100;

    private readonly ILocalisationService localisation;
    private readonly Func<IIpcClient> createClient;
    private IReadOnlyList<ClassificationTermView> terms = [];
    private IReadOnlyDictionary<Guid, string> categoryNames = new Dictionary<Guid, string>();
    private TermSortColumn sortColumn = TermSortColumn.Text;
    private bool sortDescending;
    private string filter = string.Empty;
    private TermRowViewModel? selectedTerm;
    private int filesTotal;
    private string? errorMessage;
    private string? filesErrorMessage;
    private ClassificationTab tab = ClassificationTab.Terms;

    /// <summary>Creates the screen with no terms until the first refresh.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats the numbers and times of the reclassify control.</param>
    /// <param name="createClient">Creates a fresh client per request; one client is one connection.</param>
    /// <param name="connection">Delivers the reclassify progress the daemon publishes.</param>
    public ClassificationsViewModel(ILocalisationService localisation, ICultureService culture, Func<IIpcClient> createClient, ConnectionViewModel connection)
    {
        this.localisation = localisation;
        this.createClient = createClient;
        Editor = new ClassificationEditorViewModel(localisation, createClient);
        Suggestions = new ClassificationSuggestionsViewModel(localisation, createClient);
        Reclassify = new ClassificationReclassifyViewModel(localisation, culture, createClient, connection);
        Editor.Changed += ReadAgain;
        Suggestions.Changed += ReadAgain;
        Reclassify.Finished += ReadAgain;
        LoadSuggestionsCommand = new AsyncRelayCommand(cancellationToken => Suggestions.EnsureLoadedAsync(cancellationToken));
        LoadReclassifyStatusCommand = new AsyncRelayCommand(cancellationToken => Reclassify.LoadStatusAsync(cancellationToken));
        ShowTermsCommand = new RelayCommand(() => Tab = ClassificationTab.Terms);
        ShowSuggestionsCommand = new RelayCommand(() => Tab = ClassificationTab.Suggestions);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        LoadMoreFilesCommand = new AsyncRelayCommand(cancellationToken => LoadFilesAsync(false, cancellationToken));
        ReloadFilesCommand = new AsyncRelayCommand(cancellationToken => LoadFilesAsync(true, cancellationToken));
        SortByTextCommand = new RelayCommand(() => SortBy(TermSortColumn.Text));
        SortByCategoryCommand = new RelayCommand(() => SortBy(TermSortColumn.Category));
        SortByFilesCommand = new RelayCommand(() => SortBy(TermSortColumn.Files));
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Classifications;

    /// <inheritdoc/>
    public string Title => localisation.Get("Classifications.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Classifications.Glyph");

    /// <summary>The forms that add, edit and remove categories and terms.</summary>
    public ClassificationEditorViewModel Editor { get; }

    /// <summary>The Suggestions tab.</summary>
    public ClassificationSuggestionsViewModel Suggestions { get; }

    /// <summary>The reclassify control.</summary>
    public ClassificationReclassifyViewModel Reclassify { get; }

    /// <summary>Which list is shown. Terms until changed; choosing Suggestions runs the scan the first time.</summary>
    public ClassificationTab Tab
    {
        get => tab;
        private set
        {
            if (!SetProperty(ref tab, value)) return;

            OnPropertyChanged(nameof(IsTermsTab));
            OnPropertyChanged(nameof(IsSuggestionsTab));
            if (value == ClassificationTab.Suggestions) LoadSuggestionsCommand.Execute(null);
        }
    }

    /// <summary>True while the terms are shown.</summary>
    public bool IsTermsTab => tab == ClassificationTab.Terms;

    /// <summary>True while the suggestions are shown.</summary>
    public bool IsSuggestionsTab => tab == ClassificationTab.Suggestions;

    /// <summary>Scans the file paths for suggestions the first time it runs.</summary>
    public IAsyncRelayCommand LoadSuggestionsCommand { get; }

    /// <summary>Asks the daemon whether a reclassify run is active.</summary>
    public IAsyncRelayCommand LoadReclassifyStatusCommand { get; }

    /// <summary>Shows the terms.</summary>
    public IRelayCommand ShowTermsCommand { get; }

    /// <summary>Shows the suggestions.</summary>
    public IRelayCommand ShowSuggestionsCommand { get; }

    /// <summary>Label of the terms tab.</summary>
    public string TermsTabText => localisation.Get("Classifications.Tab.Terms");

    /// <summary>Label of the suggestions tab.</summary>
    public string SuggestionsTabText => localisation.Get("Classifications.Tab.Suggestions");

    /// <summary>The terms that match <see cref="Filter"/>, in the chosen order.</summary>
    public ObservableCollection<TermRowViewModel> Terms { get; } = [];

    /// <summary>The files of <see cref="SelectedTerm"/> loaded so far.</summary>
    public ObservableCollection<FileRowViewModel> Files { get; } = [];

    /// <summary>True when no term matches, so the screen shows the "no terms" text.</summary>
    public bool IsEmpty => Terms.Count == 0;

    /// <summary>Only terms whose text or category contains this, ignoring case, are shown. Empty shows every term; never <see langword="null"/>.</summary>
    public string Filter
    {
        get => filter;
        set
        {
            if (SetProperty(ref filter, value?.Trim() ?? string.Empty)) ShowTerms();
        }
    }

    /// <summary>What the terms are ordered by. Text until changed.</summary>
    public TermSortColumn SortColumn => sortColumn;

    /// <summary>True when the order is reversed.</summary>
    public bool SortDescending => sortDescending;

    /// <summary>The term whose files are listed; <see langword="null"/> until one is chosen. Choosing one starts loading its first page of files.</summary>
    public TermRowViewModel? SelectedTerm
    {
        get => selectedTerm;
        set
        {
            if (!SetProperty(ref selectedTerm, value)) return;

            Editor.ShowTerm(value);
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(FilesHeading));
            ReloadFilesCommand.Execute(null);
        }
    }

    /// <summary>True when a term is selected.</summary>
    public bool HasSelection => selectedTerm is not null;

    /// <summary>"Files for" and the selected term; empty when none is selected.</summary>
    public string FilesHeading => selectedTerm is null ? string.Empty : localisation.Format("Classifications.Files.Heading", ("text", selectedTerm.Header));

    /// <summary>How many files of the selected term are shown and how many it has in all.</summary>
    public string FilesSummary => localisation.Format("Classifications.Files.Summary", ("shown", Files.Count), ("total", filesTotal));

    /// <summary>True when the selected term has more files than are shown, so the screen offers "show more".</summary>
    public bool CanLoadMoreFiles => Files.Count < filesTotal;

    /// <summary>True when a term is selected and has no files.</summary>
    public bool HasNoFiles => selectedTerm is not null && filesTotal == 0;

    /// <summary>Why the last terms refresh failed, worded for the user. <see langword="null"/> after one that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Why the last files request failed, worded for the user. <see langword="null"/> after one that worked.</summary>
    public string? FilesErrorMessage
    {
        get => filesErrorMessage;
        private set => SetProperty(ref filesErrorMessage, value);
    }

    /// <summary>Reads the terms again.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Loads the next page of files of the selected term.</summary>
    public IAsyncRelayCommand LoadMoreFilesCommand { get; }

    /// <summary>Drops the files shown and loads the first page of the selected term again.</summary>
    public IAsyncRelayCommand ReloadFilesCommand { get; }

    /// <summary>Orders by term text; choosing it again reverses the order.</summary>
    public IRelayCommand SortByTextCommand { get; }

    /// <summary>Orders by category; choosing it again reverses the order.</summary>
    public IRelayCommand SortByCategoryCommand { get; }

    /// <summary>Orders by file count; choosing it again reverses the order.</summary>
    public IRelayCommand SortByFilesCommand { get; }

    /// <summary>Label of the refresh button.</summary>
    public string RefreshText => localisation.Get("Classifications.Refresh");

    /// <summary>Hint inside the filter box.</summary>
    public string FilterHint => localisation.Get("Classifications.Filter.Hint");

    /// <summary>Label of the sort by term button.</summary>
    public string SortTextLabel => localisation.Get("Classifications.Sort.Text");

    /// <summary>Label of the sort by category button.</summary>
    public string SortCategoryLabel => localisation.Get("Classifications.Sort.Category");

    /// <summary>Label of the sort by file count button.</summary>
    public string SortFilesLabel => localisation.Get("Classifications.Sort.Files");

    /// <summary>Text shown when no term matches.</summary>
    public string EmptyText => localisation.Get("Classifications.Empty");

    /// <summary>Text shown while no term is selected.</summary>
    public string NoSelectionText => localisation.Get("Classifications.Files.None");

    /// <summary>Text shown when the selected term has no files.</summary>
    public string NoFilesText => localisation.Get("Classifications.Files.Empty");

    /// <summary>Label of the show more files button.</summary>
    public string MoreFilesText => localisation.Get("Classifications.Files.More");

    /// <inheritdoc/>
    public void OnNavigatedTo(string? targetId)
    {
        RefreshCommand.Execute(null);
        LoadReclassifyStatusCommand.Execute(null);
    }

    /// <summary>Reads every term from the daemon. A failure keeps the terms shown and lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        switch (await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await ListTermsAsync(client, cancellationToken),
            Fail<ConnectFailure> fail => fail
        })
        {
            case Ok<ClassificationTermsView> ok:
                terms = ok.Value.Terms;
                categoryNames = ok.Value.Categories.ToDictionary(category => category.Id, category => category.Name);
                Editor.SetCategories(ok.Value.Categories);
                ErrorMessage = null;
                ShowTerms();
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    /// <summary>Reads files of the selected term from the daemon: the first page when <paramref name="restart"/> is set, otherwise the page after those shown. A failure keeps the files shown and lands in <see cref="FilesErrorMessage"/>.</summary>
    /// <param name="restart"><see langword="true"/> to drop the files shown and start from the first page.</param>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task LoadFilesAsync(bool restart, CancellationToken cancellationToken)
    {
        if (restart) ClearFiles();
        if (selectedTerm is not { } term) return;

        await using var client = createClient();

        switch (await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await ListFilesAsync(client, term.Id, cancellationToken),
            Fail<ConnectFailure> fail => fail
        })
        {
            case Ok<TermFilesView> ok when selectedTerm == term:
                foreach (var file in ok.Value.Files) Files.Add(new FileRowViewModel(file.Path, file.AccountName));

                filesTotal = ok.Value.Total;
                FilesErrorMessage = null;
                NotifyFiles();
                break;
            case Fail<ConnectFailure> fail:
                FilesErrorMessage = fail.Error.Message;
                break;
        }
    }

    private static async Task<Result<Unit, ConnectFailure>> ConnectAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<ClassificationTermsView, ConnectFailure>> ListTermsAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetClassificationTerms(), cancellationToken) switch
        {
            Ok<ClassificationTermsView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private async Task<Result<TermFilesView, ConnectFailure>> ListFilesAsync(IIpcClient client, Guid termId, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetTermFiles(termId, Files.Count, FilePageSize), cancellationToken) switch
        {
            Ok<TermFilesView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private void ReadAgain() => RefreshCommand.Execute(null);

    private void SortBy(TermSortColumn column)
    {
        sortDescending = column == sortColumn && !sortDescending;
        sortColumn = column;
        OnPropertyChanged(nameof(SortColumn));
        OnPropertyChanged(nameof(SortDescending));
        ShowTerms();
    }

    private void ShowTerms()
    {
        var shown = terms.Where(Matches).ToList();
        var ordered = sortColumn switch
        {
            TermSortColumn.Category => shown.OrderBy(CategoryOf, StringComparer.CurrentCultureIgnoreCase).ThenBy(term => term.Text, StringComparer.CurrentCultureIgnoreCase),
            TermSortColumn.Files => shown.OrderBy(term => term.FileCount).ThenBy(term => term.Text, StringComparer.CurrentCultureIgnoreCase),
            _ => shown.OrderBy(term => term.Text, StringComparer.CurrentCultureIgnoreCase)
        };
        var selectedId = selectedTerm?.Id;
        Terms.Clear();
        foreach (var term in sortDescending ? ordered.Reverse() : ordered) Terms.Add(Row(term));

        OnPropertyChanged(nameof(IsEmpty));
        if (selectedId is { } id) SelectedTerm = Terms.FirstOrDefault(row => row.Id == id);
    }

    private bool Matches(ClassificationTermView term)
        => filter.Length == 0 || term.Text.Contains(filter, StringComparison.CurrentCultureIgnoreCase) || CategoryOf(term).Contains(filter, StringComparison.CurrentCultureIgnoreCase);

    private string CategoryOf(ClassificationTermView term) => categoryNames.GetValueOrDefault(term.CategoryId, string.Empty);

    private TermRowViewModel Row(ClassificationTermView term)
        => new(term.Id, localisation.Format(term.Ignored ? "Classifications.Term.HeaderIgnored" : "Classifications.Term.Header", ("text", term.Text)), localisation.Format("Classifications.Term.Detail", ("category", CategoryOf(term)), ("source", term.Source), ("count", term.FileCount)), term.Text, term.CategoryId, term.Ignored);

    private void ClearFiles()
    {
        Files.Clear();
        filesTotal = 0;
        FilesErrorMessage = null;
        NotifyFiles();
    }

    private void NotifyFiles()
    {
        OnPropertyChanged(nameof(FilesSummary));
        OnPropertyChanged(nameof(CanLoadMoreFiles));
        OnPropertyChanged(nameof(HasNoFiles));
    }
}
