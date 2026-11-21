using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Keeps the UI connected to the daemon, retrying with backoff, and exposes the state for the status banner.</summary>
/// <param name="createClient">Creates a fresh client per connection attempt; one client is one connection.</param>
/// <param name="initialDelay">Wait before the first retry.</param>
/// <param name="maxDelay">Upper bound the retry wait doubles towards.</param>
/// <param name="post">Runs a state change on the UI thread. Omit to run it inline.</param>
public sealed partial class ConnectionViewModel(Func<IIpcClient> createClient, TimeSpan initialDelay, TimeSpan maxDelay, Action<Action>? post = null) : ViewModelBase
{
    private readonly Action<Action> postToUi = post ?? (action => action());

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial ConnectionState State { get; set; } = ConnectionState.Disconnected;

    [ObservableProperty]
    public partial DaemonStatusView? DaemonStatus { get; set; }

    public string StatusText => State switch
    {
        ConnectionState.Connected => ApplicationConstants.ConnectedText,
        ConnectionState.Reconnecting => ApplicationConstants.ReconnectingText,
        _ =>ApplicationConstants.DisconnectedText
    };

    /// <summary>Connects, and reconnects whenever the link is lost, until cancelled. Cancellation surfaces as <see cref="OperationCanceledException"/>.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var delay = initialDelay;
        var attempted = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attempted)
            {
                SetState(ConnectionState.Reconnecting);
            }

            attempted = true;
            await using var client = createClient();

            if (await ConnectAsync(client, cancellationToken) is Ok<Unit>)
            {
                delay = initialDelay;
                SetState(ConnectionState.Connected);
                await client.Disconnected.WaitAsync(cancellationToken);
                continue;
            }

            SetState(ConnectionState.Disconnected);
            await Task.Delay(delay, cancellationToken);
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, maxDelay.Ticks));
        }
    }

    private async Task<Result<Unit, IpcError>> ConnectAsync(IIpcClient client, CancellationToken cancellationToken)
    {
        client.OnEvent<DaemonStatusChanged>(changed => postToUi(() => DaemonStatus = changed.Status));

        var connected = await client.ConnectAsync(cancellationToken);
        if (connected is Fail<IpcError>)
        {
            return connected;
        }

        return await client.QueryAsync(new GetDaemonStatus(), cancellationToken) switch
        {
            Ok<DaemonStatusView> status => await SubscribeAsync(client, status.Value, cancellationToken),
            Fail<IpcError> failed => failed
        };
    }

    private async Task<Result<Unit, IpcError>> SubscribeAsync(IIpcClient client, DaemonStatusView status, CancellationToken cancellationToken)
    {
        postToUi(() => DaemonStatus = status);

        return await client.SubscribeAsync(cancellationToken);
    }

    private void SetState(ConnectionState state) => postToUi(() => State = state);
}
