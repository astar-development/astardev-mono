using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Operations;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>The History screen: what happened. Lists the completed and failed operations the daemon reports, newest first, plus a placeholder Audit tab.</summary>
internal sealed class HistoryViewModel : OperationListViewModel, IScreen
{
    private readonly ILocalisationService localisation;
    private readonly Func<IIpcClient> createClient;
    private CancellationTokenSource? refresh;
    private string? errorMessage;
    private int tabIndex;

    /// <summary>Creates the screen on the History tab with its filters and no operations; they load each time the screen is navigated to.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date, time and number.</param>
    /// <param name="createClient">Creates a fresh client per refresh; one client is one connection.</param>
    public HistoryViewModel(ILocalisationService localisation, ICultureService culture, Func<IIpcClient> createClient)
        : base(localisation, culture, [OperationStatus.Completed, OperationStatus.Failed], true, false)
    {
        this.localisation = localisation;
        this.createClient = createClient;
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.History;

    /// <inheritdoc/>
    public string Title => localisation.Get("History.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("History.Glyph");

    /// <summary>Index of the selected tab: 0 is History, 1 is the Audit placeholder.</summary>
    public int TabIndex
    {
        get => tabIndex;
        set => SetProperty(ref tabIndex, value);
    }

    /// <summary>Text of the History tab.</summary>
    public string HistoryTabText => localisation.Get("History.Tab.History");

    /// <summary>Text of the Audit tab.</summary>
    public string AuditTabText => localisation.Get("History.Tab.Audit");

    /// <summary>Text saying the Audit tab is a placeholder: doc 19 has no Audit destination, so its place is pending confirmation.</summary>
    public string AuditPendingText => localisation.Get("History.Audit.Pending");

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
