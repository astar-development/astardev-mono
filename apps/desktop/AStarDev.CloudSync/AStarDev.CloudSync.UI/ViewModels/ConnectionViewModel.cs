using System.Collections.ObjectModel;
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

    /// <summary>Where the link is in the retry loop. Changes are posted to the UI thread, and every change also raises <see cref="StatusText"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial ConnectionState State { get; set; } = ConnectionState.Disconnected;

    /// <summary>
    /// The daemon's latest reported status: queried on each connect, then updated by <see cref="DaemonStatusChanged"/> events.
    /// <see langword="null"/> until the first connect succeeds. Not cleared when the link drops, so read it with <see cref="State"/>.
    /// </summary>
    [ObservableProperty]
    public partial DaemonStatusView? DaemonStatus { get; set; }

    /// <summary>
    /// What the daemon is doing for each job, as it reported: read once on each connect, then kept current by <see cref="ActivityChanged"/> events. Shown as received, never recalculated.
    /// Not cleared when the link drops, so a reconnect replaces it rather than flashing empty.
    /// </summary>
    public ObservableCollection<ActivityView> Activities { get; } = [];

    /// <summary>Banner text for <see cref="State"/>, so the view binds a ready-made label instead of converting the enum.</summary>
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
        client.OnEvent<ActivityChanged>(changed => postToUi(() => Upsert(changed.Activity)));

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
        if (await client.QueryAsync(new GetActivities(), cancellationToken) is Ok<ActivitiesView> activities) postToUi(() => Replace(activities.Value.Activities));

        return await client.SubscribeAsync(cancellationToken);
    }

    private void Upsert(ActivityView activity)
    {
        var index = Activities.ToList().FindIndex(existing => existing.SyncJobId == activity.SyncJobId);
        if (index < 0) Activities.Add(activity);
        else Activities[index] = activity;
    }

    private void Replace(IReadOnlyList<ActivityView> activities)
    {
        Activities.Clear();
        foreach (var activity in activities) Activities.Add(activity);
    }

    private void SetState(ConnectionState state) => postToUi(() => State = state);
}
