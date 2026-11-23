using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Operations;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>The History screen: what happened. Lists the completed and failed operations the daemon reports, newest first.</summary>
internal sealed class HistoryViewModel : OperationListViewModel, IScreen
{
    private readonly ILocalisationService localisation;
    private readonly Func<IIpcClient> createClient;
    private readonly ExportCommand exporter;
    private CancellationTokenSource? refresh;
    private string? errorMessage;

    /// <summary>Creates the screen with its filters and no operations; they load each time the screen is navigated to.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date, time and number.</param>
    /// <param name="createClient">Creates a fresh client per refresh; one client is one connection.</param>
    /// <param name="exporter">Saves the export the Export button asks for.</param>
    public HistoryViewModel(ILocalisationService localisation, ICultureService culture, Func<IIpcClient> createClient, ExportCommand exporter)
        : base(localisation, culture, [OperationStatus.Completed, OperationStatus.Failed], true)
    {
        this.localisation = localisation;
        this.createClient = createClient;
        this.exporter = exporter;
        CanExport = true;
        Export = new AsyncRelayCommand(ExportAsync);
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.History;

    /// <inheritdoc/>
    public string Title => localisation.Get("History.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("History.Glyph");

    /// <summary>Why the last refresh failed, worded for the user. <see langword="null"/> after a refresh that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Starts reloading the operations from the daemon, replacing a refresh still running, then opens the operation named by <paramref name="targetId"/> once it is listed.</summary>
    /// <param name="targetId">An operation id as text, as given by a deep link; null or unknown opens nothing.</param>
    public override void OnNavigatedTo(string? targetId)
    {
        refresh?.Cancel();
        refresh = new CancellationTokenSource();
        _ = RefreshAndOpenAsync(targetId, refresh.Token);
    }

    /// <summary>Asks the daemon for every completed and failed operation and shows them. A failure keeps the list as it was and lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and leaves the list as it was.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        switch (await ListAsync(client, cancellationToken))
        {
            case Ok<HistoryView> ok:
                Load([.. ok.Value.Entries.Select(HistoryEntries.ToEntry)]);
                ErrorMessage = null;
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    /// <summary>Saves the operations the current filters leave to a file the user chooses. A failure lands in <see cref="ErrorMessage"/>; a dismissed dialog changes nothing.</summary>
    /// <param name="cancellationToken">Cancelling abandons the dialog or the write and surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task ExportAsync(CancellationToken cancellationToken)
        => ErrorMessage = await exporter.RunAsync(new ExportHistory(CurrentFilter()), localisation.Get("History.ExportFileName"), cancellationToken) switch
        {
            Ok<Unit> => null,
            Fail<ConnectFailure> fail => fail.Error.Message
        };

    private GetHistory CurrentFilter()
        => new(SelectedProvider.Value, SelectedType.Value, SelectedStatus.Value, DateFrom is { } from ? new DateTimeOffset(from.Date, TimeSpan.Zero) : null, DateTo is { } to ? new DateTimeOffset(to.Date.AddDays(1), TimeSpan.Zero).AddTicks(-1) : null, PathText);

    private async Task RefreshAndOpenAsync(string? targetId, CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        base.OnNavigatedTo(targetId);
    }

    private static async Task<Result<HistoryView, ConnectFailure>> ListAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> => await client.QueryAsync(new GetHistory(), cancellationToken) switch
            {
                Ok<HistoryView> ok => ok,
                Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
            },
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };
}
