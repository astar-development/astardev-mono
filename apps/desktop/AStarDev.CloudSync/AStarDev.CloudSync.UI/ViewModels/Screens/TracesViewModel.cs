using System.Collections.ObjectModel;
using System.Globalization;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Traces screen: the spans the daemon stored, newest first, each with the trace it belongs to, filtered by period and trace id. The daemon keeps them across restarts, so the screen shows spans recorded before the daemon last started.
/// Spans load each time the screen is opened and whenever <see cref="RefreshCommand"/> runs.
/// </summary>
internal sealed class TracesViewModel : ViewModelBase, IScreen
{
    private static readonly TimeSpan[] WindowSpans = [TimeSpan.Zero, TimeSpan.FromHours(1), TimeSpan.FromDays(1)];
    private static readonly string[] WindowNames = ["All", "Hour", "Day"];

    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;
    private readonly Func<IIpcClient> createClient;
    private readonly TimeProvider time;
    private int windowIndex;
    private string traceId = string.Empty;
    private string? errorMessage;

    /// <summary>Creates the screen with the whole period and no trace filter, and no spans until the first refresh.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date and time.</param>
    /// <param name="createClient">Creates a fresh client per refresh; one client is one connection.</param>
    /// <param name="time">The clock the period filter is measured from.</param>
    public TracesViewModel(ILocalisationService localisation, ICultureService culture, Func<IIpcClient> createClient, TimeProvider time)
    {
        this.localisation = localisation;
        this.culture = culture;
        this.createClient = createClient;
        this.time = time;
        Windows = WindowNames.Select(name => localisation.Get($"Traces.Window.{name}")).ToList();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Traces;

    /// <inheritdoc/>
    public string Title => localisation.Get("Traces.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Traces.Glyph");

    /// <summary>The stored spans that match the filters, newest first.</summary>
    public ObservableCollection<SpanRowViewModel> Spans { get; } = [];

    /// <summary>True when the last refresh found nothing, so the screen shows the "no spans" text.</summary>
    public bool IsEmpty => Spans.Count == 0;

    /// <summary>Texts of the period filter: all time, last hour, last 24 hours.</summary>
    public IReadOnlyList<string> Windows { get; }

    /// <summary>Index into <see cref="Windows"/>: spans older than the period are left out. Zero, all time, until changed; an out-of-range value is ignored.</summary>
    public int WindowIndex
    {
        get => windowIndex;
        set
        {
            if (value >= 0 && value < WindowSpans.Length) SetProperty(ref windowIndex, value);
        }
    }

    /// <summary>Only spans of this trace are shown. Empty shows every trace; never <see langword="null"/>.</summary>
    public string TraceId
    {
        get => traceId;
        set => SetProperty(ref traceId, value?.Trim() ?? string.Empty);
    }

    /// <summary>Why the last refresh failed, worded for the user. <see langword="null"/> after a refresh that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Reads the spans again with the current filters.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Label of the trace id filter.</summary>
    public string TraceIdLabel => localisation.Get("Traces.TraceId.Label");

    /// <summary>Label of the refresh button.</summary>
    public string RefreshText => localisation.Get("Traces.Refresh");

    /// <summary>Text shown when no span matches.</summary>
    public string EmptyText => localisation.Get("Traces.Empty");

    /// <inheritdoc/>
    public void OnNavigatedTo(string? targetId) => RefreshCommand.Execute(null);

    /// <summary>Reads the spans that match the filters from the daemon. A failure keeps the spans shown and lands in <see cref="ErrorMessage"/>.</summary>
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

    private async Task<Result<TracesView, ConnectFailure>> ListAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetTraces(WindowSpans[windowIndex] == TimeSpan.Zero ? default : time.GetUtcNow() - WindowSpans[windowIndex], traceId), cancellationToken) switch
        {
            Ok<TracesView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private void Show(Result<TracesView, ConnectFailure> result)
    {
        switch (result)
        {
            case Ok<TracesView> ok:
                Spans.Clear();
                foreach (var span in ok.Value.Spans) Spans.Add(Row(span));
                OnPropertyChanged(nameof(IsEmpty));
                ErrorMessage = null;
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private SpanRowViewModel Row(SpanView span)
        => new(Header(span), localisation.Format("Traces.Row.Ids", ("trace", span.TraceId), ("span", span.SpanId), ("parent", span.ParentSpanId.Length == 0 ? localisation.Get("Traces.Root") : span.ParentSpanId)), span.Detail);

    private string Header(SpanView span)
        => localisation.Format(span.Failed ? "Traces.Row.Failed" : "Traces.Row", ("date", culture.FormatDate(span.StartUtc)), ("time", culture.FormatTime(span.StartUtc)), ("name", span.Name), ("duration", span.DurationMilliseconds.ToString("0.##", CultureInfo.CurrentCulture)));
}
