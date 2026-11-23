using System.Collections.ObjectModel;
using System.Globalization;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Metrics screen: for each instrument the daemon sampled in the last hour, its current value and its recent history. The daemon keeps the samples across restarts, so the history spans daemon runs.
/// Samples load each time the screen is opened and whenever <see cref="RefreshCommand"/> runs.
/// </summary>
internal sealed class MetricsViewModel : ViewModelBase, IScreen
{
    private const int HistoryLength = 20;
    private static readonly TimeSpan HistoryPeriod = TimeSpan.FromHours(1);

    private readonly ILocalisationService localisation;
    private readonly Func<IIpcClient> createClient;
    private readonly TimeProvider time;
    private string? errorMessage;

    /// <summary>Creates the screen with no instruments until the first refresh.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="createClient">Creates a fresh client per refresh; one client is one connection.</param>
    /// <param name="time">The clock the history period is measured from.</param>
    public MetricsViewModel(ILocalisationService localisation, Func<IIpcClient> createClient, TimeProvider time)
    {
        this.localisation = localisation;
        this.createClient = createClient;
        this.time = time;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Metrics;

    /// <inheritdoc/>
    public string Title => localisation.Get("Metrics.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Metrics.Glyph");

    /// <summary>One row per instrument sampled in the last hour, ordered by name.</summary>
    public ObservableCollection<MetricRowViewModel> Rows { get; } = [];

    /// <summary>True when the last refresh found no samples, so the screen shows the "no samples" text.</summary>
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>Why the last refresh failed, worded for the user. <see langword="null"/> after a refresh that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Reads the samples again.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Label of the refresh button.</summary>
    public string RefreshText => localisation.Get("Metrics.Refresh");

    /// <summary>Text shown when no sample exists.</summary>
    public string EmptyText => localisation.Get("Metrics.Empty");

    /// <inheritdoc/>
    public void OnNavigatedTo(string? targetId) => RefreshCommand.Execute(null);

    /// <summary>Reads the last hour of samples from the daemon. A failure keeps the rows shown and lands in <see cref="ErrorMessage"/>.</summary>
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

    private static string Number(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private async Task<Result<MetricsView, ConnectFailure>> ListAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetMetrics(time.GetUtcNow() - HistoryPeriod), cancellationToken) switch
        {
            Ok<MetricsView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private void Show(Result<MetricsView, ConnectFailure> result)
    {
        switch (result)
        {
            case Ok<MetricsView> ok:
                Rows.Clear();
                foreach (var row in ok.Value.Samples.GroupBy(sample => sample.Name).OrderBy(group => group.Key, StringComparer.Ordinal).Select(Row)) Rows.Add(row);
                OnPropertyChanged(nameof(IsEmpty));
                ErrorMessage = null;
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private MetricRowViewModel Row(IGrouping<string, MetricSampleView> samples)
    {
        var newestFirst = samples.OrderByDescending(sample => sample.TimestampUtc).ToList();

        return new MetricRowViewModel(samples.Key, localisation.Format("Metrics.Row.Current", ("value", Number(newestFirst[0].Value)), ("unit", newestFirst[0].Unit)), string.Join(' ', newestFirst.Take(HistoryLength).Reverse().Select(sample => Number(sample.Value))));
    }
}
