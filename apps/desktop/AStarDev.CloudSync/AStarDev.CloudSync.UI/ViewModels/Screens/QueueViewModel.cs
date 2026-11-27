using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Operations;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>The Queue screen: the daemon's dead-lettered operations with Retry and Ignore and a pointer to the Conflicts screen. It never resolves conflicts.</summary>
internal sealed class QueueViewModel : OperationListViewModel, IScreen
{
    private readonly ILocalisationService localisation;
    private readonly ICultureService culture;
    private readonly Func<IIpcClient> createClient;
    private readonly ConnectionViewModel connection;
    private string? errorMessage;

    /// <summary>Creates the screen with no filters and starts following the dead letters and the queue the daemon reported.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats every date, time and number.</param>
    /// <param name="createClient">Creates a fresh client per decision; one client is one connection.</param>
    /// <param name="connection">Holds the dead letters and the queue the daemon reported; the screen rebuilds its rows whenever they change.</param>
    public QueueViewModel(ILocalisationService localisation, ICultureService culture, Func<IIpcClient> createClient, ConnectionViewModel connection)
        : base(localisation, culture, [OperationStatus.Pending, OperationStatus.Running], false)
    {
        this.localisation = localisation;
        this.culture = culture;
        this.createClient = createClient;
        this.connection = connection;
        ConflictsNote = localisation.Get("Queue.ConflictsNote");
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync);
        connection.DeadLetters.CollectionChanged += (_, _) => Rebuild();
        Rebuild();
        FollowQueue(connection, ShowCounts);
    }

    /// <inheritdoc/>
    public ScreenId Id => ScreenId.Queue;

    /// <inheritdoc/>
    public string Title => localisation.Get("Queue.Title");

    /// <inheritdoc/>
    public string Glyph => localisation.Get("Queue.Glyph");

    /// <summary>How many operations are in each status, as colour, icon and text, from the daemon's counts. Statuses with none are left out.</summary>
    public IReadOnlyList<StatusIndicatorViewModel> StatusCounts { get; private set; } = [];

    /// <summary>Text pointing the user to the Conflicts screen, the only place conflicts are resolved.</summary>
    public string ConflictsNote { get; }

    /// <summary>The operations that gave up loaded so far, in claim order, each with Retry and Ignore.</summary>
    public ObservableCollection<DeadLetterRowViewModel> DeadLetters { get; } = [];

    /// <summary>Whether any operation waits for a decision, so the section can hide itself when none does.</summary>
    public bool HasDeadLetters => DeadLetters.Count > 0;

    /// <summary>Reads the page of dead letters after those shown and adds it below them.</summary>
    public IAsyncRelayCommand LoadMoreCommand { get; }

    /// <summary>True while the daemon holds more dead letters than those shown, so the view offers <see cref="LoadMoreCommand"/>.</summary>
    public bool CanLoadMore => DeadLetters.Count < connection.DeadLettersTotal;

    /// <summary>Label of the button that loads more dead letters.</summary>
    public string MoreText => localisation.Get("Queue.DeadLetters.More");

    /// <summary>Heading of the dead-letter section.</summary>
    public string DeadLettersHeading => localisation.Get("Queue.DeadLetters.Heading");

    /// <summary>Label of the button that tries an operation again.</summary>
    public string RetryText => localisation.Get("Queue.DeadLetters.Retry");

    /// <summary>Label of the button that drops an operation.</summary>
    public string IgnoreText => localisation.Get("Queue.DeadLetters.Ignore");

    /// <summary>Why the last decision failed, worded for the user. <see langword="null"/> after a decision that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Asks the daemon to try a dead-lettered operation again, then shows the dead letters that remain. A refusal lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="operationId">The operation to retry.</param>
    /// <param name="cancellationToken">Cancelling abandons the wait; the daemon may still finish the request.</param>
    public Task RetryAsync(Guid operationId, CancellationToken cancellationToken) => DecideAsync(new RetryOperation(operationId), cancellationToken);

    /// <summary>Asks the daemon to drop a dead-lettered operation, then shows the dead letters that remain. A refusal lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="operationId">The operation to drop.</param>
    /// <param name="cancellationToken">Cancelling abandons the wait; the daemon may still finish the request.</param>
    public Task IgnoreAsync(Guid operationId, CancellationToken cancellationToken) => DecideAsync(new IgnoreOperation(operationId), cancellationToken);

    /// <summary>Reads the page of dead letters after those shown and adds it to them. A failure keeps the dead letters shown and lands in <see cref="ErrorMessage"/>.</summary>
    /// <param name="cancellationToken">Cancelling abandons the wait and surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task LoadMoreAsync(CancellationToken cancellationToken)
    {
        await using var client = createClient();

        switch (await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await ListAsync(client, connection.DeadLetters.Count, cancellationToken),
            Fail<ConnectFailure> fail => fail
        })
        {
            case Ok<DeadLettersView> ok:
                connection.AddDeadLetters(ok.Value);
                ErrorMessage = null;
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private async Task DecideAsync(ICommand command, CancellationToken cancellationToken)
    {
        await using var client = createClient();

        Show(await ConnectAsync(client, cancellationToken) switch
        {
            Ok<Unit> => await SendAndListAsync(client, command, cancellationToken),
            Fail<ConnectFailure> fail => fail
        });
    }

    private static async Task<Result<Unit, ConnectFailure>> ConnectAsync(IIpcClient client, CancellationToken cancellationToken)
        => await client.ConnectAsync(cancellationToken) switch
        {
            Ok<Unit> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<DeadLettersView, ConnectFailure>> SendAndListAsync(IIpcClient client, ICommand command, CancellationToken cancellationToken)
        => await client.SendAsync(command, cancellationToken) switch
        {
            Ok<CommandResult> { Value.Outcome: CommandOutcome.Accepted } => await ListAsync(client, 0, cancellationToken),
            Ok<CommandResult> rejected => new Fail<ConnectFailure>(new ConnectFailure(rejected.Value.Message ?? ApplicationConstants.RequestRejectedText)),
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private static async Task<Result<DeadLettersView, ConnectFailure>> ListAsync(IIpcClient client, int skip, CancellationToken cancellationToken)
        => await client.QueryAsync(new GetDeadLetters(skip), cancellationToken) switch
        {
            Ok<DeadLettersView> ok => ok,
            Fail<IpcError> fail => new Fail<ConnectFailure>(ConnectFailure.From(fail.Error))
        };

    private void Show(Result<DeadLettersView, ConnectFailure> result)
    {
        switch (result)
        {
            case Ok<DeadLettersView> ok:
                connection.ReplaceDeadLetters(ok.Value);
                ErrorMessage = null;
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }

    private void ShowCounts(QueueView queue)
    {
        StatusCounts = [.. new[] { (OperationStatus.Pending, queue.Pending), (OperationStatus.Running, queue.Running), (OperationStatus.Completed, queue.Completed), (OperationStatus.Failed, queue.Failed) }.Where(count => count.Item2 > 0).Select(count => new StatusIndicatorViewModel(OperationStatusLevel.Of(count.Item1), $"{localisation.Get($"Operations.Status.{count.Item1}")}: {culture.FormatNumber(count.Item2)}"))];
        OnPropertyChanged(nameof(StatusCounts));
    }

    private void Rebuild()
    {
        DeadLetters.Clear();
        foreach (var deadLetter in connection.DeadLetters) DeadLetters.Add(new DeadLetterRowViewModel(deadLetter, localisation, culture, this));
        OnPropertyChanged(nameof(HasDeadLetters));
        OnPropertyChanged(nameof(CanLoadMore));
    }
}
