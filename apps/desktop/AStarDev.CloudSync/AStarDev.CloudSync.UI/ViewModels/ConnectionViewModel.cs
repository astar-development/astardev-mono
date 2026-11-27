using System.Collections.ObjectModel;
using System.Diagnostics;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>Keeps the UI connected to the daemon, retrying with backoff, and exposes the state for the status banner.</summary>
/// <param name="createClient">Creates a fresh client per connection attempt; one client is one connection.</param>
/// <param name="initialDelay">Wait before the first retry.</param>
/// <param name="maxDelay">Upper bound the retry wait doubles towards.</param>
/// <param name="post">Runs a state change on the UI thread. Omit to run it inline.</param>
/// <param name="startupTimeout">How long the first connect may take before <see cref="StartupTimedOut"/> is set. Omit for 30 seconds. Checked after each failed attempt, so it can be exceeded by up to one retry wait.</param>
public sealed partial class ConnectionViewModel(Func<IIpcClient> createClient, TimeSpan initialDelay, TimeSpan maxDelay, Action<Action>? post = null, TimeSpan? startupTimeout = null) : ViewModelBase, IStatusSource
{
    private static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromSeconds(30);

    private readonly Action<Action> postToUi = post ?? (action => action());
    private readonly Dictionary<Guid, int> conflictTotals = [];
    private readonly HashSet<Guid> replacedAway = [];

    /// <summary>Where the link is in the retry loop. Changes are posted to the UI thread, and every change also raises <see cref="StatusText"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial ConnectionState State { get; set; } = ConnectionState.Disconnected;

    /// <summary>True from the first successful connect and never reset, so a later lost link does not bring back the starting up screen.</summary>
    [ObservableProperty]
    public partial bool HasConnected { get; set; }

    /// <summary>True when no connect succeeded within the startup timeout. Cleared by the first successful connect.</summary>
    [ObservableProperty]
    public partial bool StartupTimedOut { get; set; }

    /// <summary>
    /// The daemon's latest reported status: queried on each connect, then updated by <see cref="DaemonStatusChanged"/> events.
    /// <see langword="null"/> until the first connect succeeds. Not cleared when the link drops, so read it with <see cref="State"/>.
    /// </summary>
    [ObservableProperty]
    public partial DaemonStatusView? DaemonStatus { get; set; }

    /// <summary>The values of the status bar: read once on each connect, then kept current by <see cref="SyncStatusChanged"/> events. Empty until the first read succeeds; not cleared when the link drops, so read it with <see cref="State"/>.</summary>
    [ObservableProperty]
    public partial Option<SyncStatusView> SyncStatus { get; set; } = Option.None<SyncStatusView>();

    /// <summary>
    /// What the daemon is doing for each job, as it reported: read once on each connect, then kept current by <see cref="ActivityChanged"/> events. Shown as received, never recalculated.
    /// Not cleared when the link drops, so a reconnect replaces it rather than flashing empty.
    /// </summary>
    public ObservableCollection<ActivityView> Activities { get; } = [];

    /// <summary>
    /// The pages of the operations that gave up and wait for a retry or ignore decision loaded so far: the first page read once on each connect, then replaced by the Queue screen after each decision and grown by its load more. Shown as received.
    /// Not cleared when the link drops, so a reconnect replaces it rather than flashing empty.
    /// </summary>
    public ObservableCollection<DeadLetterView> DeadLetters { get; } = [];

    /// <summary>How many operations gave up in all, as the daemon last reported; more than <c>DeadLetters.Count</c> while pages are still to load. Set before <see cref="DeadLetters"/> changes.</summary>
    public int DeadLettersTotal { get; private set; }

    /// <summary>
    /// The queue as the daemon reported it: read once on each connect, then replaced by each <see cref="QueueChanged"/> event. Holds the counts per status and the pending and running operations, shown as received.
    /// Empty until the first read succeeds; not cleared when the link drops, so a reconnect replaces it rather than flashing empty.
    /// </summary>
    [ObservableProperty]
    public partial QueueView Queue { get; set; } = new(0, 0, 0, 0, []);

    /// <summary>
    /// The pages of the conflicts that wait for the user loaded so far, deferred ones included, across every job: the first page of each job read once on each connect, then kept current by <see cref="ConflictDetected"/> and <see cref="ConflictResolved"/> events,
    /// replaced by the Conflicts screen after each decision and grown by its load more. A detected conflict is added only when its job has no page left to load, so the next page still starts in the right place.
    /// A deferred conflict raises no event, so a decision to defer shows here only through that replacement. Shown as received, not cleared when the link drops.
    /// </summary>
    public ObservableCollection<ConflictView> Conflicts { get; } = [];

    /// <summary>How many conflicts wait for the user across every job, loaded or not; more than <c>Conflicts.Count</c> while pages are still to load. Raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> on every change, before <see cref="Conflicts"/> changes.</summary>
    public int ConflictsTotal => conflictTotals.Values.Sum();

    /// <summary>
    /// Raised on the UI thread for each daemon event pushed while connected, before the event changes <see cref="DaemonStatus"/>, <see cref="Activities"/>, <see cref="Queue"/> or <see cref="Conflicts"/>, so a listener can still read the previous value.
    /// The reads done on each connect do not raise it.
    /// </summary>
    internal event Action<IEvent>? EventReceived;

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
        var started = Stopwatch.GetTimestamp();

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
                postToUi(() =>
                {
                    HasConnected = true;
                    StartupTimedOut = false;
                });
                SetState(ConnectionState.Connected);
                await client.Disconnected.WaitAsync(cancellationToken);
                continue;
            }

            SetState(ConnectionState.Disconnected);
            if (!HasConnected && Stopwatch.GetElapsedTime(started) >= (startupTimeout ?? DefaultStartupTimeout)) postToUi(() => StartupTimedOut = true);
            await Task.Delay(delay, cancellationToken);
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, maxDelay.Ticks));
        }
    }

    private async Task<Result<Unit, IpcError>> ConnectAsync(IIpcClient client, CancellationToken cancellationToken)
    {
        client.OnEvent<DaemonStatusChanged>(changed => postToUi(() =>
        {
            EventReceived?.Invoke(changed);
            DaemonStatus = changed.Status;
        }));
        client.OnEvent<SyncStatusChanged>(changed => postToUi(() =>
        {
            EventReceived?.Invoke(changed);
            SyncStatus = Option.Some(changed.Status);
        }));
        client.OnEvent<ActivityChanged>(changed => postToUi(() =>
        {
            EventReceived?.Invoke(changed);
            Upsert(changed.Activity);
        }));
        client.OnEvent<ConflictDetected>(detected => postToUi(() =>
        {
            EventReceived?.Invoke(detected);
            Detect(detected.Conflict);
        }));
        client.OnEvent<ConflictResolved>(resolved => postToUi(() =>
        {
            EventReceived?.Invoke(resolved);
            Resolve(resolved);
        }));
        client.OnEvent<DashboardChanged>(changed => postToUi(() => EventReceived?.Invoke(changed)));
        client.OnEvent<QueueChanged>(changed => postToUi(() =>
        {
            EventReceived?.Invoke(changed);
            Queue = changed.Queue;
        }));
        client.OnEvent<CrawlProgressChanged>(progress => postToUi(() => EventReceived?.Invoke(progress)));
        client.OnEvent<ReclassifyProgressChanged>(progress => postToUi(() => EventReceived?.Invoke(progress)));

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
        if (await client.QueryAsync(new GetSyncStatus(), cancellationToken) is Ok<SyncStatusView> syncStatus) postToUi(() => SyncStatus = Option.Some(syncStatus.Value));
        if (await client.QueryAsync(new GetActivities(), cancellationToken) is Ok<ActivitiesView> activities) postToUi(() => Replace(activities.Value.Activities));
        if (await client.QueryAsync(new GetDeadLetters(), cancellationToken) is Ok<DeadLettersView> deadLetters) postToUi(() => ReplaceDeadLetters(deadLetters.Value));
        if (await client.QueryAsync(new GetQueue(), cancellationToken) is Ok<QueueView> queue) postToUi(() => Queue = queue.Value);
        if (await ConflictListing.ListAsync(client, cancellationToken) is Ok<IReadOnlyDictionary<Guid, ConflictsView>> conflicts) postToUi(() => ReplaceConflicts(conflicts.Value));

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

    internal void ReplaceDeadLetters(DeadLettersView page)
    {
        DeadLettersTotal = page.Total;
        DeadLetters.Clear();
        foreach (var deadLetter in page.DeadLetters) DeadLetters.Add(deadLetter);
    }

    internal void AddDeadLetters(DeadLettersView page)
    {
        DeadLettersTotal = page.Total;
        var shown = DeadLetters.Select(deadLetter => deadLetter.OperationId).ToHashSet();
        foreach (var deadLetter in page.DeadLetters.Where(deadLetter => !shown.Contains(deadLetter.OperationId))) DeadLetters.Add(deadLetter);
    }

    internal void ReplaceConflicts(IReadOnlyDictionary<Guid, ConflictsView> pages)
    {
        conflictTotals.Clear();
        AddConflicts(pages, true);
    }

    internal void AddConflicts(IReadOnlyDictionary<Guid, ConflictsView> pages) => AddConflicts(pages, false);

    internal int LoadedConflicts(Guid syncJobId) => Conflicts.Count(conflict => conflict.SyncJobId == syncJobId);

    private void AddConflicts(IReadOnlyDictionary<Guid, ConflictsView> pages, bool replace)
    {
        foreach (var (jobId, page) in pages) conflictTotals[jobId] = page.Total;
        OnPropertyChanged(nameof(ConflictsTotal));
        var conflicts = pages.Values.SelectMany(page => page.Conflicts).OrderBy(conflict => conflict.DetectedUtc).ToList();
        if (replace)
        {
            var kept = conflicts.Select(conflict => conflict.ConflictId).ToHashSet();
            foreach (var gone in Conflicts.Where(conflict => !kept.Contains(conflict.ConflictId)).ToList())
            {
                replacedAway.Add(gone.ConflictId);
                Conflicts.Remove(gone);
            }
        }

        foreach (var conflict in conflicts) Upsert(conflict);
    }

    private void Detect(ConflictView conflict)
    {
        if (Conflicts.Any(existing => existing.ConflictId == conflict.ConflictId))
        {
            Upsert(conflict);
            return;
        }

        var total = conflictTotals.GetValueOrDefault(conflict.SyncJobId);
        var fullyLoaded = LoadedConflicts(conflict.SyncJobId) >= total;
        conflictTotals[conflict.SyncJobId] = total + 1;
        OnPropertyChanged(nameof(ConflictsTotal));
        if (fullyLoaded) Conflicts.Add(conflict);
    }

    private void Resolve(ConflictResolved resolved)
    {
        if (replacedAway.Remove(resolved.ConflictId)) return;

        conflictTotals[resolved.SyncJobId] = Math.Max(conflictTotals.GetValueOrDefault(resolved.SyncJobId) - 1, 0);
        OnPropertyChanged(nameof(ConflictsTotal));
        Remove(resolved.ConflictId);
    }

    private void Upsert(ConflictView conflict)
    {
        var index = Conflicts.ToList().FindIndex(existing => existing.ConflictId == conflict.ConflictId);
        if (index < 0) Conflicts.Add(conflict);
        else if (Conflicts[index] != conflict) Conflicts[index] = conflict;
    }

    private void Remove(Guid conflictId)
    {
        var existing = Conflicts.FirstOrDefault(conflict => conflict.ConflictId == conflictId);
        if (existing is not null) Conflicts.Remove(existing);
    }

    private void SetState(ConnectionState state) => postToUi(() => State = state);
}
