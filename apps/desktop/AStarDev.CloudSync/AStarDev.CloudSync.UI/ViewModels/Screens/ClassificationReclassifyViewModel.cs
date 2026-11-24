using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The reclassify control of the Classifications screen: a button that asks the daemon to recompute every file's automatic links, and the progress the daemon publishes while it runs.
/// A run that was started elsewhere, or by the daemon at startup, shows the same way: the screen asks for the status when it opens and then follows <see cref="ReclassifyProgressChanged"/>.
/// <see cref="Finished"/> is raised when a run ends, so the screen can read the file counts again.
/// </summary>
internal sealed class ClassificationReclassifyViewModel : ViewModelBase
{
    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;
    private readonly Func<IIpcClient> createClient;
    private bool running;
    private long filesDone;
    private DateTimeOffset? lastCompleted;
    private string? errorMessage;

    /// <summary>Creates the control idle, with no run known until the first status.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every number, date and time.</param>
    /// <param name="createClient">Creates a fresh client per request; one client is one connection.</param>
    /// <param name="connection">Delivers the progress events the daemon publishes.</param>
    public ClassificationReclassifyViewModel(ILocalisationService localisation, ICultureService culture, Func<IIpcClient> createClient, ConnectionViewModel connection)
    {
        this.localisation = localisation;
        this.culture = culture;
        this.createClient = createClient;
        StartCommand = new AsyncRelayCommand(StartAsync, () => !running);
        connection.EventReceived += OnEvent;
    }

    /// <summary>Raised when a run ends.</summary>
    public event Action? Finished;

    /// <summary>True while a run goes on.</summary>
    public bool IsRunning => running;

    /// <summary>The state of the last or the active run as text: how far it has got, when the last one finished, or that none finished yet.</summary>
    public string StatusText => running
        ? localisation.Format("Classifications.Reclassify.Running", ("count", culture.FormatNumber(filesDone)))
        : lastCompleted is { } at
            ? localisation.Format("Classifications.Reclassify.Done", ("count", culture.FormatNumber(filesDone)), ("date", culture.FormatDate(at)), ("time", culture.FormatTime(at)))
            : localisation.Get("Classifications.Reclassify.Never");

    /// <summary>Why the last request failed, worded for the user. <see langword="null"/> after one that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Asks the daemon to start a run. It is unavailable while one is active.</summary>
    public IAsyncRelayCommand StartCommand { get; }

    /// <summary>Label of the start button.</summary>
    public string StartText => localisation.Get("Classifications.Reclassify.Start");

    /// <summary>Asks the daemon whether a run is active and when the last one finished. A failure lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task LoadStatusAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        switch (await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> => await StatusAsync(client, cancellationToken),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        })
        {
            case Ok<ReclassifyStatusView> ok:
                running = ok.Value.Running;
                filesDone = ok.Value.FilesDone;
                lastCompleted = ok.Value.LastCompletedUtc;
                ErrorMessage = null;
                Notify();
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private static async Task<Result<ReclassifyStatusView, ConnectFailure>> StatusAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetReclassifyStatus(), cancellationToken) switch
        {
            Ok<ReclassifyStatusView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        switch (await ClassificationRequests.SendAsync(createClient, new StartReclassify(), localisation.Get("Classifications.Editor.Refused"), cancellationToken))
        {
            case Ok<Unit>:
                running = true;
                filesDone = 0;
                ErrorMessage = null;
                Notify();
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private void OnEvent(IEvent @event)
    {
        if (@event is not ReclassifyProgressChanged progress) return;

        var ended = running && !progress.Running;
        running = progress.Running;
        filesDone = progress.FilesDone;
        if (ended) lastCompleted = DateTimeOffset.UtcNow;

        Notify();
        if (ended) Finished?.Invoke();
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(StatusText));
        StartCommand.NotifyCanExecuteChanged();
    }
}
