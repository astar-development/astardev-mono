using System.Collections.ObjectModel;
using AStarDev.CloudSync.UI.Localisation;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Operations;

/// <summary>
/// The shared body of the Queue, Activity and History screens: a list of sample operations narrowed by provider, type, status, date range and path,
/// a details panel, a disabled export and a placeholder retention. Replacing the sample source with daemon data later does not change the view.
/// </summary>
internal abstract class OperationListViewModel : ViewModelBase
{
    private readonly IReadOnlyList<OperationRowViewModel> allRows;
    private FilterChoice selectedProvider;
    private FilterChoice selectedType;
    private FilterChoice selectedStatus;
    private DateTime? dateFrom;
    private DateTime? dateTo;
    private string pathText = string.Empty;

    /// <summary>Creates the list from the sample operations whose status is in <paramref name="statuses"/>.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date, time and number.</param>
    /// <param name="statuses">The statuses this screen shows; the status filter offers only these.</param>
    /// <param name="showTools">False hides filters, export and retention (the Queue is a read-only overview).</param>
    protected OperationListViewModel(ILocalisationService localisation, ICultureService culture, IReadOnlyCollection<OperationStatus> statuses, bool showTools)
    {
        ShowTools = showTools;
        allRows = SampleOperations.All.Where(entry => statuses.Contains(entry.Status)).OrderByDescending(entry => entry.Timestamp).Select(entry => new OperationRowViewModel(entry, localisation, culture)).ToList();
        var all = new FilterChoice(localisation.Get("Operations.All"), null);
        ProviderChoices = [all, .. allRows.Select(row => row.Entry.Provider).Distinct().Select(name => new FilterChoice(name, name))];
        TypeChoices = [all, .. allRows.Select(row => row.Entry.Type).Distinct().Select(type => new FilterChoice(localisation.Get($"Operations.Type.{type}"), type.ToString()))];
        StatusChoices = [all, .. statuses.Select(status => new FilterChoice(localisation.Get($"Operations.Status.{status}"), status.ToString()))];
        selectedProvider = selectedType = selectedStatus = all;
        SampleDataText = localisation.Get("Operations.SampleData");
        ExportText = localisation.Get("Operations.Export");
        ExportDisabledReason = localisation.Get("Operations.ExportDisabled");
        RetentionText = localisation.Get("Operations.Retention");
        NoMatchesText = localisation.Get("Operations.NoMatches");
        ClearFilters = new RelayCommand(Clear);
        Rows = [.. allRows];
    }

    /// <summary>The operations passing every filter, newest first. Updated in place when a filter changes.</summary>
    public ObservableCollection<OperationRowViewModel> Rows { get; }

    /// <summary>The panel showing one operation beside the list.</summary>
    public OperationDetailsViewModel Details { get; } = new();

    /// <summary>True when filters, export and retention are shown.</summary>
    public bool ShowTools { get; }

    /// <summary>Provider filter entries; the first is "All".</summary>
    public IReadOnlyList<FilterChoice> ProviderChoices { get; }

    /// <summary>Operation type filter entries; the first is "All".</summary>
    public IReadOnlyList<FilterChoice> TypeChoices { get; }

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

    /// <summary>Text saying the list holds sample data, not real operations.</summary>
    public string SampleDataText { get; }

    /// <summary>Label of the export button, which is always disabled for now.</summary>
    public string ExportText { get; }

    /// <summary>Why export is disabled.</summary>
    public string ExportDisabledReason { get; }

    /// <summary>The retention period as a placeholder; not yet configurable.</summary>
    public string RetentionText { get; }

    /// <summary>Text shown when no row passes the filters.</summary>
    public string NoMatchesText { get; }

    /// <summary>Opens the details panel for the row whose operation id is <paramref name="targetId"/>; an unknown or <see langword="null"/> id leaves the panel as it was.</summary>
    public void OnNavigatedTo(string? targetId)
    {
        var match = allRows.FirstOrDefault(row => row.Entry.Id == targetId);
        if (match is not null) Details.Open(match);
    }

    private void Set<T>(ref T field, T value)
    {
        if (!SetProperty(ref field, value)) return;

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
