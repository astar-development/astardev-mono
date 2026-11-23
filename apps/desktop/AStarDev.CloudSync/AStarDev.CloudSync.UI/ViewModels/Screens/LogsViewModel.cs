using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Logs screen: the log entries the daemon stored, newest first, filtered by level, period and text. The daemon keeps them across restarts, so the screen shows entries written before the daemon last started.
/// Entries load each time the screen is opened and whenever <see cref="RefreshCommand"/> runs.
/// </summary>
internal sealed class LogsViewModel : ViewModelBase, IScreen
{
    private static readonly TimeSpan[] WindowSpans = [TimeSpan.Zero, TimeSpan.FromHours(1), TimeSpan.FromDays(1)];
    private static readonly string[] WindowNames = ["All", "Hour", "Day"];

    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;
    private readonly Func<IIpcClient> createClient;
    private readonly TimeProvider time;
    private int minimumLevel;
    private int windowIndex;
    private string text = string.Empty;
    private string? errorMessage;

    /// <summary>Creates the screen with every level, the whole period and no text filter, and no entries until the first refresh.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date and time.</param>
    /// <param name="createClient">Creates a fresh client per refresh; one client is one connection.</param>
    /// <param name="time">The clock the period filter is measured from.</param>
    public LogsViewModel(ILocalisationService localisation, ICultureService culture, Func<IIpcClient> createClient, TimeProvider time)
    {
        this.localisation = localisation;
        this.culture = culture;
        this.createClient = createClient;
        this.time = time;
        Levels = Enumerable.Range(0, 6).Select(level => localisation.Get($"Logs.Level.{level}")).ToList();
        Windows = WindowNames.Select(name => localisation.Get($"Logs.Window.{name}")).ToList();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Logs;

    /// <inheritdoc/>
    public string Title => localisation.Get("Logs.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Logs.Glyph");

    /// <summary>The stored entries that match the filters, newest first.</summary>
    public ObservableCollection<LogRowViewModel> Entries { get; } = [];

    /// <summary>True when the last refresh found nothing, so the screen shows the "no entries" text.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>Texts of the level filter, Trace first; the index is the minimum <c>LogLevel</c> number.</summary>
    public IReadOnlyList<string> Levels { get; }

    /// <summary>Texts of the period filter: all time, last hour, last 24 hours.</summary>
    public IReadOnlyList<string> Windows { get; }

    /// <summary>Index into <see cref="Levels"/>: entries below this level are left out. Zero until changed.</summary>
    public int MinimumLevel
    {
        get => minimumLevel;
        set => SetProperty(ref minimumLevel, Math.Clamp(value, 0, Levels.Count - 1));
    }

    /// <summary>Index into <see cref="Windows"/>: entries older than the period are left out. Zero, all time, until changed; an out-of-range value is ignored.</summary>
    public int WindowIndex
    {
        get => windowIndex;
        set
        {
            if (value >= 0 && value < WindowSpans.Length) SetProperty(ref windowIndex, value);
        }
    }

    /// <summary>Entries whose message does not contain this text, ignoring case, are left out. Never <see langword="null"/>.</summary>
    public string Text
    {
        get => text;
        set => SetProperty(ref text, value ?? string.Empty);
    }

    /// <summary>Why the last refresh failed, worded for the user. <see langword="null"/> after a refresh that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Reads the entries again with the current filters.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Label of the level filter.</summary>
    public string LevelLabel => localisation.Get("Logs.Level.Label");

    /// <summary>Label of the period filter.</summary>
    public string WindowLabel => localisation.Get("Logs.Window.Label");

    /// <summary>Label of the text filter.</summary>
    public string TextLabel => localisation.Get("Logs.Text.Label");

    /// <summary>Label of the refresh button.</summary>
    public string RefreshText => localisation.Get("Logs.Refresh");

    /// <summary>Text shown when no entry matches.</summary>
    public string EmptyText => localisation.Get("Logs.Empty");

    /// <inheritdoc/>
    public void OnNavigatedTo(string? targetId) => RefreshCommand.Execute(null);

    /// <summary>Reads the entries that match the filters from the daemon. A failure keeps the entries shown and lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        Show(await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await ListAsync(client, cancellationToken),
            Fail<ConnectFailure> fail => fail
        });
    }

    private static async Task<Result<Unit, ConnectFailure>> ConnectAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private async Task<Result<LogsView, ConnectFailure>> ListAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetLogs(minimumLevel, WindowSpans[windowIndex] == TimeSpan.Zero ? default : time.GetUtcNow() - WindowSpans[windowIndex], text), cancellationToken) switch
        {
            Ok<LogsView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private void Show(Result<LogsView, ConnectFailure> result)
    {
        switch (result)
        {
            case Ok<LogsView> ok:
                Entries.Clear();
                foreach (var entry in ok.Value.Entries) Entries.Add(Row(entry));
                OnPropertyChanged(nameof(IsEmpty));
                ErrorMessage = null;
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private LogRowViewModel Row(LogEntryView entry)
        => new(localisation.Format("Logs.Row", ("date", culture.FormatDate(entry.TimestampUtc)), ("time", culture.FormatTime(entry.TimestampUtc)), ("level", localisation.Get($"Logs.Level.Name.{Math.Clamp(entry.Level, 0, 5)}")), ("source", entry.Source)), entry.Message);
}
