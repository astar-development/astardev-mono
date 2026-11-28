using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.Notifications;
using AStarDev.CloudSync.UI.Tray;
using AStarDev.CloudSync.UI.ViewModels;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class DaemonNotificationBridgeShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeShell : IMainWindowHost, IApplicationExit
    {
        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Exit()
        {
        }
    }

    private sealed class RecordingPresenter : INotificationPresenter
    {
        public List<Notification> Presented { get; } = [];

        public void Present(Notification notification) => Presented.Add(notification);
    }

    private sealed class Rig : IAsyncDisposable
    {
        private readonly CancellationTokenSource source;
        private readonly Task run;

        private Rig(FakeIpcClient client, ConnectionViewModel connection, AccountsViewModel accounts, RecordingPresenter presenter, CancellationTokenSource source)
        {
            Client = client;
            Connection = connection;
            Accounts = accounts;
            Presenter = presenter;
            this.source = source;
            run = connection.RunAsync(source.Token);
        }

        public FakeIpcClient Client { get; }

        public ConnectionViewModel Connection { get; }

        public AccountsViewModel Accounts { get; }

        public RecordingPresenter Presenter { get; }

        public static async Task<Rig> StartAsync(FakeIpcClient client)
        {
            var shell = new FakeShell();
            var navigation = new NavigationService(Enum.GetValues<ScreenId>().Select(id => new FakeScreen(id)));
            var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<AStarDev.FunctionalParadigm.Ok<IReadOnlyDictionary<string, string>>>().Value;
            var localisation = new LocalisationService(new CultureService("en-GB"), new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
            var presenter = new RecordingPresenter();
            var service = new NotificationService(presenter, new TrayLifecycle(shell, navigation, shell), localisation, TimeProvider.System, TimeSpan.FromMinutes(5));
            var connection = new ConnectionViewModel(() => client, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
            var accounts = new AccountsViewModel(() => client);
            _ = new DaemonNotificationBridge(connection, accounts, service);
            var source = CancellationTokenSource.CreateLinkedTokenSource(Token);
            var rig = new Rig(client, connection, accounts, presenter, source);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (connection.State != ConnectionState.Connected && DateTime.UtcNow < deadline) await Task.Delay(5, Token);
            await Task.Delay(50, Token);

            return rig;
        }

        public async ValueTask DisposeAsync()
        {
            await source.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => run);
        }
    }

    private static ActivityView Activity(Guid job, ActivityState state, bool actionRequired = false) => new(job, "OneDrive", state, 10, 100, 10, null, Stamp, Stamp, null, actionRequired);

    private static ConflictView Conflict() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ModifyModify", Stamp, "Open", "a.txt", null, null);

    [Fact]
    public async Task notify_once_for_a_conflict_detected_and_deep_link_to_it()
    {
        var client = new FakeIpcClient([], available: true);
        await using var rig = await Rig.StartAsync(client);
        var conflict = Conflict();

        client.Raise(new ConflictDetected(conflict));

        var notification = rig.Presenter.Presented.Single();
        notification.Target.ShouldBe(new NotificationTarget(ScreenId.Conflicts, conflict.ConflictId.ToString()));
    }

    [Fact]
    public async Task notify_when_a_scope_completes_but_not_while_it_progresses()
    {
        var client = new FakeIpcClient([], available: true);
        await using var rig = await Rig.StartAsync(client);
        var job = Guid.NewGuid();

        client.Raise(new CrawlProgressChanged(job, "root-1", 500, false));
        rig.Presenter.Presented.ShouldBeEmpty();
        client.Raise(new CrawlProgressChanged(job, "root-1", 900, true));

        rig.Presenter.Presented.Single().Title.ShouldBe("Folder scanned");
    }

    [Fact]
    public async Task notify_only_the_first_of_every_ten_completed_scopes()
    {
        var client = new FakeIpcClient([], available: true);
        await using var rig = await Rig.StartAsync(client);
        var job = Guid.NewGuid();

        foreach (var root in Enumerable.Range(1, 21))
            client.Raise(new CrawlProgressChanged(job, $"root-{root}", 900, true));

        rig.Presenter.Presented.Select(notification => notification.Key).ShouldBe(["SyncScopeComplete:root-1", "SyncScopeComplete:root-11", "SyncScopeComplete:root-21"]);
    }

    [Fact]
    public async Task not_count_conflicts_towards_the_completed_scopes_left_unnotified()
    {
        var client = new FakeIpcClient([], available: true);
        await using var rig = await Rig.StartAsync(client);
        var job = Guid.NewGuid();
        var conflict = Conflict();

        client.Raise(new CrawlProgressChanged(job, "root-1", 900, true));
        client.Raise(new ConflictDetected(conflict));
        foreach (var root in Enumerable.Range(2, 10))
            client.Raise(new CrawlProgressChanged(job, $"root-{root}", 900, true));

        rig.Presenter.Presented.Select(notification => notification.Key).ShouldBe(["SyncScopeComplete:root-1", $"ConflictDetected:{conflict.ConflictId}", "SyncScopeComplete:root-11"]);
    }

    [Fact]
    public async Task notify_when_the_initial_crawl_of_a_job_completes()
    {
        var job = Guid.NewGuid();
        var client = new FakeIpcClient([], available: true) { ActivitiesReply = new AStarDev.FunctionalParadigm.Ok<ActivitiesView>(new ActivitiesView([Activity(job, ActivityState.InitialIndexing)])) };
        await using var rig = await Rig.StartAsync(client);

        client.Raise(new ActivityChanged(Activity(job, ActivityState.InitialIndexing) with { ItemsDone = 60 }));
        rig.Presenter.Presented.ShouldBeEmpty();
        client.Raise(new ActivityChanged(Activity(job, ActivityState.Healthy)));

        rig.Presenter.Presented.Single().Title.ShouldBe("First scan complete");
    }

    [Fact]
    public async Task not_notify_for_routine_activity_changes()
    {
        var job = Guid.NewGuid();
        var client = new FakeIpcClient([], available: true) { ActivitiesReply = new AStarDev.FunctionalParadigm.Ok<ActivitiesView>(new ActivitiesView([Activity(job, ActivityState.Working)])) };
        await using var rig = await Rig.StartAsync(client);

        client.Raise(new ActivityChanged(Activity(job, ActivityState.Healthy)));
        client.Raise(new ActivityChanged(Activity(job, ActivityState.Waiting)));
        client.Raise(new DaemonStatusChanged(FakeIpcClient.Status));
        client.Raise(new ConflictResolved(Guid.NewGuid(), job, Guid.NewGuid(), "KeepLocal", Stamp));
        client.Raise(new AccountConnected("me@example.com"));

        rig.Presenter.Presented.ShouldBeEmpty();
    }

    [Fact]
    public async Task notify_once_when_a_job_starts_needing_the_user()
    {
        var job = Guid.NewGuid();
        var client = new FakeIpcClient([], available: true) { ActivitiesReply = new AStarDev.FunctionalParadigm.Ok<ActivitiesView>(new ActivitiesView([Activity(job, ActivityState.Working)])) };
        await using var rig = await Rig.StartAsync(client);

        client.Raise(new ActivityChanged(Activity(job, ActivityState.Working, actionRequired: true)));
        client.Raise(new ActivityChanged(Activity(job, ActivityState.Working, actionRequired: true) with { ItemsDone = 70 }));

        rig.Presenter.Presented.Single().Title.ShouldBe("Action needed");
    }

    [Fact]
    public async Task notify_once_per_account_whose_sign_in_expired_until_it_signs_in_again()
    {
        var expired = new AccountView(Guid.NewGuid(), "me@example.com", "OneDrive", AccountState.ReauthenticationRequired);
        var client = new FakeIpcClient([], available: true) { AccountsReply = new AStarDev.FunctionalParadigm.Ok<AccountsView>(new AccountsView([expired])) };
        await using var rig = await Rig.StartAsync(client);

        await rig.Accounts.RefreshAsync(Token);
        await rig.Accounts.RefreshAsync(Token);

        var notification = rig.Presenter.Presented.Single();
        notification.Title.ShouldBe("Sign-in expired");
        notification.Target.ShouldBe(new NotificationTarget(ScreenId.Providers, expired.Id.ToString()));
    }

    [Fact]
    public async Task not_notify_for_an_account_that_works()
    {
        var client = new FakeIpcClient([], available: true) { AccountsReply = new AStarDev.FunctionalParadigm.Ok<AccountsView>(new AccountsView([new AccountView(Guid.NewGuid(), "me@example.com", "OneDrive", AccountState.Connected)])) };
        await using var rig = await Rig.StartAsync(client);

        await rig.Accounts.RefreshAsync(Token);

        rig.Presenter.Presented.ShouldBeEmpty();
    }

    [Fact]
    public async Task stop_when_the_connection_run_is_cancelled()
    {
        var client = new FakeIpcClient([], available: true);
        var rig = await Rig.StartAsync(client);

        await rig.DisposeAsync();

        rig.Connection.State.ShouldBe(ConnectionState.Connected);
    }
}
