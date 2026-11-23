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
    private string? errorMessage;

    /// <summary>Creates the screen with no entries until the first refresh.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date and time.</param>
    /// <param name="createClient">Creates a fresh client per refresh; one client is one connection.</param>
    /// <param name="time">The clock the end of the listed range is measured from.</param>
    public AuditViewModel(ILocalisationService localisation, ICultureService culture, Func<IIpcClient> createClient, TimeProvider time)
    {
        this.localisation = localisation;
        this.culture = culture;
        this.createClient = createClient;
        this.time = time;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Audit;

    /// <inheritdoc/>
    public string Title => localisation.Get("Audit.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Audit.Glyph");

    /// <summary>The recorded entries, newest first. Each row's message is the entry's details as JSON.</summary>
    public ObservableCollection<LogRowViewModel> Entries { get; } = [];

    /// <summary>True when the last refresh found nothing, so the screen shows the "nothing recorded" text.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>Why the last refresh failed, worded for the user. <see langword="null"/> after a refresh that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Reads the entries again.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Label of the refresh button.</summary>
    public string RefreshText => localisation.Get("Audit.Refresh");

    /// <summary>Text shown when nothing has been recorded.</summary>
    public string EmptyText => localisation.Get("Audit.Empty");

    /// <inheritdoc/>
    public void OnNavigatedTo(string? targetId) => RefreshCommand.Execute(null);

    /// <summary>Reads every kept entry from the daemon. A failure keeps the entries shown and lands in <see cref="ErrorMessage"/>.</summary>
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

    private async Task<Result<AuditView, ConnectFailure>> ListAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetAudit(DateTimeOffset.UnixEpoch, time.GetUtcNow().AddDays(1), []), cancellationToken) switch
        {
            Ok<AuditView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private void Show(Result<AuditView, ConnectFailure> result)
    {
        switch (result)
        {
            case Ok<AuditView> ok:
                Entries.Clear();
                foreach (var entry in ok.Value.Entries.OrderByDescending(entry => entry.OccurredUtc)) Entries.Add(Row(entry));
                OnPropertyChanged(nameof(IsEmpty));
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
