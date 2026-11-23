using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>
/// The shared body of the Queue, Activity and History screens: a list of operations narrowed by provider, type, status, date range and path,
/// a details panel and an export that screens opt into. Starts empty; a screen fills it with <see cref="Load"/>.
/// </summary>
internal abstract class OperationListViewModel : ViewModelBase
{
    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;
    private readonly FilterChoice all;
    private IReadOnlyList<OperationRowViewModel> allRows = [];
    private IReadOnlyList<FilterChoice> providerChoices = [];
    private IReadOnlyList<FilterChoice> typeChoices = [];
    private string noMatchesText = string.Empty;
    private FilterChoice selectedProvider;
    private FilterChoice selectedType;
    private FilterChoice selectedStatus;
    private DateTime? dateFrom;
    private DateTime? dateTo;
    private string pathText = string.Empty;

    /// <summary>Creates an empty list for the operations whose status is in <paramref name="statuses"/>.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date, time and number.</param>
    /// <param name="statuses">The statuses this screen shows; the status filter offers only these.</param>
    /// <param name="showTools">False hides filters and export (the Queue is a read-only overview).</param>
    protected OperationListViewModel(ILocalisationService localisation, ICultureService culture, IReadOnlyCollection<OperationStatus> statuses, bool showTools)
    {
        this.localisation = localisation;
        this.culture = culture;
        ShowTools = showTools;
        all = new FilterChoice(localisation.Get("Operations.All"), null);
        StatusChoices = [all, .. statuses.Select(status => new FilterChoice(localisation.Get($"Operations.Status.{status}"), status.ToString()))];
        selectedProvider = selectedType = selectedStatus = all;
        ExportText = localisation.Get("Operations.Export");
        ExportDisabledReason = localisation.Get("Operations.ExportDisabled");
        ClearFilters = new RelayCommand(Clear);
        Load([]);
    }

    /// <summary>The operations passing every filter, newest first. Updated in place when a filter changes.</summary>
    public ObservableCollection<OperationRowViewModel> Rows { get; } = [];

    /// <summary>The panel showing one operation beside the list.</summary>
    public OperationDetailsViewModel Details { get; } = new();

    /// <summary>True when filters and export are shown.</summary>
    public bool ShowTools { get; }

    /// <summary>Provider filter entries; the first is "All".</summary>
    public IReadOnlyList<FilterChoice> ProviderChoices => providerChoices;

    /// <summary>Operation type filter entries; the first is "All".</summary>
    public IReadOnlyList<FilterChoice> TypeChoices => typeChoices;

    /// <summary>Status filter entries; the first is "All".</summary>
    public IReadOnlyList<FilterChoice> StatusChoices { get; }

    /// <summary>The chosen provider filter.</summary>
    public FilterChoice SelectedProvider
    {
        get => selectedProvider;
        set => Set(ref selectedProvider, value);
    }

    /// <summary>The chosen operation type filter.</summary>
    public FilterChoice SelectedType
    {
        get => selectedType;
        set => Set(ref selectedType, value);
    }

    /// <summary>The chosen status filter.</summary>
    public FilterChoice SelectedStatus
    {
        get => selectedStatus;
        set => Set(ref selectedStatus, value);
    }

    /// <summary>First day to include, compared with the date of each operation; <see langword="null"/> is unbounded.</summary>
    public DateTime? DateFrom
    {
        get => dateFrom;
        set => Set(ref dateFrom, value);
    }

    /// <summary>Last day to include, inclusive; <see langword="null"/> is unbounded.</summary>
    public DateTime? DateTo
    {
        get => dateTo;
        set => Set(ref dateTo, value);
    }

    /// <summary>Text the affected path must contain, ignoring case; blank matches every path.</summary>
    public string PathText
    {
        get => pathText;
        set => Set(ref pathText, value ?? string.Empty);
    }

    /// <summary>Resets every filter, so every row shows again.</summary>
    public IRelayCommand ClearFilters { get; }

    /// <summary>True when filters leave no row.</summary>
    public bool HasNoRows => Rows.Count == 0;

    /// <summary>Label of the export button.</summary>
    public string ExportText { get; }

    /// <summary>True when the screen can export, so the button is enabled and <see cref="ExportDisabledReason"/> is hidden.</summary>
    public bool CanExport { get; protected set; }

    /// <summary>Exports what the screen shows. Does nothing on a screen that cannot export.</summary>
    public IAsyncRelayCommand Export { get; protected set; } = new AsyncRelayCommand(() => Task.CompletedTask);

    /// <summary>Why export is disabled.</summary>
    public string ExportDisabledReason { get; }

    /// <summary>Text shown when no row shows: that no operation exists yet, or that the filters leave none.</summary>
    public string NoMatchesText
    {
        get => noMatchesText;
        private set => SetProperty(ref noMatchesText, value);
    }

    /// <summary>Opens the details panel for the row whose operation id is <paramref name="targetId"/>; an unknown or <see langword="null"/> id leaves the panel as it was.</summary>
    public virtual void OnNavigatedTo(string? targetId)
    {
        var match = allRows.FirstOrDefault(row => row.Entry.Id == targetId);
        if (match is not null) Details.Open(match);
    }

    /// <summary>Replaces every operation the list can show, keeping each filter that still has a matching choice and resetting the others to "All".</summary>
    /// <param name="entries">The operations in any order; they show newest first.</param>
    protected void Load(IReadOnlyList<OperationEntry> entries)
    {
        allRows = entries.OrderByDescending(entry => entry.Timestamp).Select(entry => new OperationRowViewModel(entry, localisation, culture)).ToList();
        providerChoices = [all, .. allRows.Select(row => row.Entry.Provider).Distinct().Select(name => new FilterChoice(name, name))];
        typeChoices = [all, .. allRows.Select(row => row.Entry.Type).Distinct().Select(type => new FilterChoice(localisation.Get($"Operations.Type.{type}"), type.ToString()))];
        selectedProvider = Keep(providerChoices, selectedProvider);
        selectedType = Keep(typeChoices, selectedType);
        NoMatchesText = localisation.Get(entries.Count == 0 ? "Operations.Empty" : "Operations.NoMatches");
        OnPropertyChanged(nameof(ProviderChoices));
        OnPropertyChanged(nameof(TypeChoices));
        OnPropertyChanged(nameof(SelectedProvider));
        OnPropertyChanged(nameof(SelectedType));
        Refilter();
    }

    /// <summary>Shows the pending and running operations the daemon reported, now and after every change of <see cref="ConnectionViewModel.Queue"/>.</summary>
    /// <param name="connection">Holds the queue the daemon reported.</param>
    /// <param name="changed">Called with the queue each time it is shown, so a screen can show more than the operations.</param>
    protected void FollowQueue(ConnectionViewModel connection, Action<QueueView> changed)
    {
        void Show()
        {
            Load([.. connection.Queue.Operations.Select(QueueEntries.ToEntry)]);
            changed(connection.Queue);
        }

        connection.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ConnectionViewModel.Queue)) Show();
        };
        Show();
    }

    private FilterChoice Keep(IReadOnlyList<FilterChoice> choices, FilterChoice selected) => choices.FirstOrDefault(choice => choice.Value == selected.Value) ?? all;

    private void Set<T>(ref T field, T value)
    {
        if (!SetProperty(ref field, value)) return;

        Refilter();
    }

    private void Refilter()
    {
        var rows = allRows.Where(Passes).ToList();
        Rows.Clear();
        foreach (var row in rows) Rows.Add(row);
        OnPropertyChanged(nameof(HasNoRows));
    }

    private void Clear()
    {
        SelectedProvider = ProviderChoices[0];
        SelectedType = TypeChoices[0];
        SelectedStatus = StatusChoices[0];
        DateFrom = null;
        DateTo = null;
        PathText = string.Empty;
    }

    private bool Passes(OperationRowViewModel row)
    {
        var entry = row.Entry;

        return Matches(selectedProvider, entry.Provider)
            && Matches(selectedType, entry.Type.ToString())
            && Matches(selectedStatus, entry.Status.ToString())
            && (dateFrom is null || entry.Timestamp.Date >= dateFrom.Value.Date)
            && (dateTo is null || entry.Timestamp.Date <= dateTo.Value.Date)
            && entry.Path.Contains(pathText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(FilterChoice choice, string value) => choice.Value is null || choice.Value == value;
}
