using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The Diagnostics screen: the daemon's version, state and uptime and the status of every health check it has registered. The daemon sends no file path and no account name, so nothing here needs hiding.
/// Diagnostics load each time the screen is opened and whenever <see cref="RefreshCommand"/> runs.
/// </summary>
internal sealed class DiagnosticsViewModel : ViewModelBase, IScreen
{
    private readonly ILocalisationService localisation;
    private readonly Func<IIpcClient> createClient;
    private readonly TimeProvider time;
    private string version = string.Empty;
    private string state = string.Empty;
    private string uptime = string.Empty;
    private string? errorMessage;

    /// <summary>Creates the screen with no diagnostics until the first refresh.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="createClient">Creates a fresh client per refresh; one client is one connection.</param>
    /// <param name="time">The clock the uptime is measured against.</param>
    public DiagnosticsViewModel(ILocalisationService localisation, Func<IIpcClient> createClient, TimeProvider time)
    {
        this.localisation = localisation;
        this.createClient = createClient;
        this.time = time;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Diagnostics;

    /// <inheritdoc/>
    public string Title => localisation.Get("Diagnostics.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Diagnostics.Glyph");

    /// <summary>The daemon's version text; empty until a refresh has worked.</summary>
    public string Version
    {
        get => version;
        private set => SetProperty(ref version, value);
    }

    /// <summary>What the daemon is doing, as a localised label; empty until a refresh has worked.</summary>
    public string State
    {
        get => state;
        private set => SetProperty(ref state, value);
    }

    /// <summary>How long the daemon has been running as of the last refresh, in days, hours and minutes; empty until a refresh has worked.</summary>
    public string Uptime
    {
        get => uptime;
        private set => SetProperty(ref uptime, value);
    }

    /// <summary>One row per registered health check, ordered by name.</summary>
    public ObservableCollection<HealthCheckRowViewModel> Checks { get; } = [];

    /// <summary>Why the last refresh failed, worded for the user. <see langword="null"/> after a refresh that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Reads the diagnostics again.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Label of the version line.</summary>
    public string VersionLabel => localisation.Get("Diagnostics.Version.Label");

    /// <summary>Label of the state line.</summary>
    public string StateLabel => localisation.Get("Diagnostics.State.Label");

    /// <summary>Label of the uptime line.</summary>
    public string UptimeLabel => localisation.Get("Diagnostics.Uptime.Label");

    /// <summary>Heading of the health check list.</summary>
    public string ChecksLabel => localisation.Get("Diagnostics.Checks.Label");

    /// <summary>Label of the refresh button.</summary>
    public string RefreshText => localisation.Get("Diagnostics.Refresh");

    /// <inheritdoc/>
    public void OnNavigatedTo(string? targetId) => RefreshCommand.Execute(null);

    /// <summary>Reads the diagnostics from the daemon. A failure keeps what is shown and lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        Show(await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await QueryAsync(client, cancellationToken),
            Fail<ConnectFailure> fail => fail
        });
    }

    private static async Task<Result<Unit, ConnectFailure>> ConnectAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<DiagnosticsView, ConnectFailure>> QueryAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetDiagnostics(), cancellationToken) switch
        {
            Ok<DiagnosticsView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private void Show(Result<DiagnosticsView, ConnectFailure> result)
    {
        switch (result)
        {
            case Ok<DiagnosticsView> ok:
                Version = ok.Value.Version;
                State = localisation.Get($"Diagnostics.State.{ok.Value.State}");
                Uptime = UptimeText(time.GetUtcNow() - ok.Value.StartedUtc);
                Checks.Clear();
                foreach (var check in ok.Value.Checks) Checks.Add(new HealthCheckRowViewModel(check.Name, localisation.Get($"Diagnostics.Status.{check.Status}")));
                ErrorMessage = null;
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private string UptimeText(TimeSpan elapsed)
        => localisation.Format("Diagnostics.Uptime", ("days", elapsed.Days), ("hours", elapsed.Hours), ("minutes", elapsed.Minutes));
}
