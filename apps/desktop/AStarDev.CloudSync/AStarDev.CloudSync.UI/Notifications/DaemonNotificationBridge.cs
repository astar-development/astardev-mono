using System.Collections.Specialized;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels;

namespace AStarDev.CloudSync.UI.Notifications;

/// <summary>
/// Turns what the daemon reports into the notifications doc 13 lists: conflict detected, authentication expired, initial crawl complete, sync scope complete and user action required.
/// Every other event, and every report that repeats a state already announced, raises nothing. Constructing it starts the listening, so resolve it once at startup.
/// </summary>
internal sealed class DaemonNotificationBridge
{
    private readonly HashSet<Guid> expiredAccounts = [];
    private readonly ConnectionViewModel connection;
    private readonly INotificationService notifications;

    /// <summary>Starts listening to the daemon events and to the account list.</summary>
    /// <param name="connection">Source of daemon events and of the previous activity of a job.</param>
    /// <param name="accounts">Source of accounts whose sign-in expired; each is announced once until it is listed as signed in again.</param>
    /// <param name="notifications">Receives the events to notify.</param>
    public DaemonNotificationBridge(ConnectionViewModel connection, AccountsViewModel accounts, INotificationService notifications)
    {
        this.connection = connection;
        this.notifications = notifications;
        connection.EventReceived += OnEvent;
        accounts.Accounts.CollectionChanged += OnAccountsChanged;
    }

    private void OnEvent(IEvent daemonEvent)
    {
        switch (daemonEvent)
        {
            case ConflictDetected detected:
                Raise(NotificationEventKind.ConflictDetected, detected.Conflict.ConflictId);
                break;
            case CrawlProgressChanged { Completed: true } progress:
                notifications.Raise(new NotificationEvent(NotificationEventKind.SyncScopeComplete, progress.RemoteRootId));
                break;
            case ActivityChanged changed:
                AnnounceActivity(changed.Activity, PreviousOf(changed.Activity));
                break;
        }
    }

    private ActivityView? PreviousOf(ActivityView activity) => connection.Activities.FirstOrDefault(known => known.SyncJobId == activity.SyncJobId);

    private void AnnounceActivity(ActivityView current, ActivityView? previous)
    {
        if (previous is { State: ActivityState.InitialIndexing } && current.State != ActivityState.InitialIndexing) Raise(NotificationEventKind.InitialCrawlComplete, current.SyncJobId);
        if (current.ActionRequired && previous is not { ActionRequired: true }) Raise(NotificationEventKind.UserActionRequired, current.SyncJobId);
    }

    private void OnAccountsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        foreach (var card in args.NewItems?.OfType<AccountCardViewModel>() ?? []) AnnounceIfExpired(card.Account);
    }

    private void AnnounceIfExpired(AccountView account)
    {
        if (account.State != AccountState.ReauthenticationRequired) expiredAccounts.Remove(account.Id);
        else if (expiredAccounts.Add(account.Id)) Raise(NotificationEventKind.AuthenticationExpired, account.Id);
    }

    private void Raise(NotificationEventKind kind, Guid subject) => notifications.Raise(new NotificationEvent(kind, subject.ToString()));
}
