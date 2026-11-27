using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Audit screen: every significant action the daemon recorded, newest first, as proof that it happened. The daemon prunes entries past the audit retention setting, so the screen only ever shows what is still kept.
/// Entries load each time the screen is opened and whenever <see cref="RefreshCommand"/> runs.
/// </summary>
internal sealed class AuditViewModel : ViewModelBase, IScreen
{
    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;
    private readonly Func<IIpcClient> createClient;
    private readonly TimeProvider time;
    private readonly ExportCommand exporter;
    private string? errorMessage;
    private int total;
    private int version;

    /// <summary>Creates the screen with no entries until the first refresh.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date and time.</param>
    /// <param name="createClient">Creates a fresh client per refresh; one client is one connection.</param>
    /// <param name="time">The clock the end of the listed range is measured from.</param>
    /// <param name="exporter">Saves the export the Export button asks for.</param>
    public AuditViewModel(ILocalisationService localisation, ICultureService culture, Func<IIpcClient> createClient, TimeProvider time, ExportCommand exporter)
    {
        this.exporter = exporter;
        this.localisation = localisation;
        this.culture = culture;
        this.createClient = createClient;
        this.time = time;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        LoadMoreCommand = new AsyncRelayCommand(cancellationToken => LoadAsync(false, cancellationToken));
        ExportCommand = new AsyncRelayCommand(ExportAsync);
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Audit;

    /// <inheritdoc/>
    public string Title => localisation.Get("Audit.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Audit.Glyph");

    /// <summary>The recorded entries loaded so far, newest first. Each row's message is the entry's details as JSON.</summary>
    public ObservableCollection<LogRowViewModel> Entries { get; } = [];

    /// <summary>True when the last refresh found nothing, so the screen shows the "nothing recorded" text.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>Why the last refresh failed, worded for the user. <see langword="null"/> after a refresh that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Reads the first page of entries again, replacing those shown.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Reads the page of older entries after those shown and adds it below them.</summary>
    public IAsyncRelayCommand LoadMoreCommand { get; }

    /// <summary>True while the daemon keeps older entries than those shown, so the view offers <see cref="LoadMoreCommand"/>.</summary>
    public bool CanLoadMore => Entries.Count < total;

    /// <summary>Label of the button that loads older entries.</summary>
    public string MoreText => localisation.Get("Audit.More");

    /// <summary>Saves the entries to a file the user chooses.</summary>
    public IAsyncRelayCommand ExportCommand { get; }

    /// <summary>Label of the export button.</summary>
    public string ExportText => localisation.Get("Audit.Export");

    /// <summary>Label of the refresh button.</summary>
    public string RefreshText => localisation.Get("Audit.Refresh");

    /// <summary>Text shown when nothing has been recorded.</summary>
    public string EmptyText => localisation.Get("Audit.Empty");

    /// <inheritdoc/>
    public void OnNavigatedTo(string? targetId) => RefreshCommand.Execute(null);

    /// <summary>Reads the newest page of kept entries from the daemon, replacing those shown. A failure keeps the entries shown and lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    public Task RefreshAsync(CancellationToken cancellationToken) => LoadAsync(true, cancellationToken);

    /// <summary>Reads a page of entries: the newest when <paramref name="restart"/> is set, otherwise the one after those shown. A reply that a newer refresh has replaced is dropped. A failure keeps the entries shown and lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="restart"><see langword="true"/> to replace the entries shown with the newest page.</param>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task LoadAsync(bool restart, CancellationToken cancellationToken)
    {
        var asked = restart ? ++version : version;
        var skip = restart ? 0 : Entries.Count;
        await using var client = createClient();

        var result = await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await ListAsync(client, skip, cancellationToken),
            Fail<ConnectFailure> fail => fail
        };
        if (asked == version) Show(result, restart);
    }

    /// <summary>Saves every kept entry to a file the user chooses. A failure lands in <see cref="ErrorMessage"/>; a dismissed dialog changes nothing.</summary>
    /// <param name="cancellationToken">Cancelling abandons the dialog or the write and surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task ExportAsync(CancellationToken cancellationToken)
        => ErrorMessage = await exporter.RunAsync(new ExportAudit(Everything()), localisation.Get("Audit.ExportFileName"), cancellationToken) switch
        {
            Ok<Unit> => null,
            Fail<ConnectFailure> fail => fail.Error.Message
        };

    private GetAudit Everything() => new(DateTimeOffset.UnixEpoch, time.GetUtcNow().AddDays(1), []);

    private GetAudit Page(int skip) => Everything() with { Skip = skip };

    private static async Task<Result<Unit, ConnectFailure>> ConnectAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private async Task<Result<AuditView, ConnectFailure>> ListAsync(IIpcClient client, int skip, CancellationToken cancellationToken)
        => await client.QueryAsync(Page(skip), cancellationToken) switch
        {
            Ok<AuditView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private void Show(Result<AuditView, ConnectFailure> result, bool restart)
    {
        switch (result)
        {
            case Ok<AuditView> ok:
                if (restart) Entries.Clear();
                foreach (var entry in ok.Value.Entries) Entries.Add(Row(entry));
                total = ok.Value.Total;
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(CanLoadMore));
                ErrorMessage = null;
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private LogRowViewModel Row(AuditEntryView entry)
        => new(localisation.Format("Audit.Row", ("date", culture.FormatDate(entry.OccurredUtc)), ("time", culture.FormatTime(entry.OccurredUtc)), ("type", entry.EventType)), entry.DetailsJson);
}
