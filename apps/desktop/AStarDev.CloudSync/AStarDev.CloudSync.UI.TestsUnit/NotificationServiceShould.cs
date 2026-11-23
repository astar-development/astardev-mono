using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.Notifications;
using AStarDev.CloudSync.UI.Tray;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class NotificationServiceShould
{
    private sealed class FakeShell : IMainWindowHost, IApplicationExit
    {
        public int Shown { get; private set; }

        public void Show() => Shown++;

        public void Hide()
        {
        }

        public void Exit()
        {
        }
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }

    private sealed class RecordingPresenter : INotificationPresenter
    {
        public List<Notification> Presented { get; } = [];

        public void Present(Notification notification) => Presented.Add(notification);
    }

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    public static TheoryData<string> NotifyingKinds => new(Enum.GetNames<NotificationEventKind>().Where(name => name != nameof(NotificationEventKind.SyncSucceeded)));

    private static LocalisationService RealLocalisation()
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(new CultureService("en-GB"), new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    private static (NotificationService Service, RecordingPresenter Presenter, FakeClock Clock, FakeShell Shell, NavigationService Navigation) Create()
    {
        var shell = new FakeShell();
        var navigation = new NavigationService(Enum.GetValues<ScreenId>().Select(id => new FakeScreen(id)));
        var presenter = new RecordingPresenter();
        var clock = new FakeClock();
        var service = new NotificationService(presenter, new TrayLifecycle(shell, navigation, shell), RealLocalisation(), clock, Window);

        return (service, presenter, clock, shell, navigation);
    }

    [Fact]
    public void aggregate_repeated_events()
    {
        var (service, presenter, _, _, _) = Create();

        service.Raise(new NotificationEvent(NotificationEventKind.ItemFailed, "file-1"));
        service.Raise(new NotificationEvent(NotificationEventKind.ItemFailed, "file-1"));
        service.Raise(new NotificationEvent(NotificationEventKind.ItemFailed, "file-1"));

        presenter.Presented.Count.ShouldBe(1);
        service.Active.Single().Count.ShouldBe(3);
        service.Active.Single().Message.ShouldContain("3");
    }

    [Fact]
    public void notify_again_once_the_aggregation_window_has_passed()
    {
        var (service, presenter, clock, _, _) = Create();
        service.Raise(new NotificationEvent(NotificationEventKind.ItemFailed, "file-1"));

        clock.Advance(Window + TimeSpan.FromSeconds(1));
        service.Raise(new NotificationEvent(NotificationEventKind.ItemFailed, "file-1"));

        presenter.Presented.Count.ShouldBe(2);
        service.Active.Single().Count.ShouldBe(1);
    }

    [Fact]
    public void not_aggregate_events_about_different_subjects()
    {
        var (service, presenter, _, _, _) = Create();

        service.Raise(new NotificationEvent(NotificationEventKind.ItemFailed, "file-1"));
        service.Raise(new NotificationEvent(NotificationEventKind.ItemFailed, "file-2"));

        presenter.Presented.Count.ShouldBe(2);
    }

    [Fact]
    public void not_notify_routine_success()
    {
        var (service, presenter, _, _, _) = Create();

        service.Raise(new NotificationEvent(NotificationEventKind.SyncSucceeded, "sync-1"));

        presenter.Presented.ShouldBeEmpty();
        service.Active.ShouldBeEmpty();
    }

    [Fact]
    public void honour_the_notification_preferences()
    {
        var (service, presenter, _, _, _) = Create();

        service.Preferences = new NotificationPreferences(Conflicts: false, Errors: false, Completion: true);
        service.Raise(new NotificationEvent(NotificationEventKind.ConflictDetected, "c-1"));
        service.Raise(new NotificationEvent(NotificationEventKind.ItemFailed, "f-1"));
        service.Raise(new NotificationEvent(NotificationEventKind.AuthenticationExpired, "a-1"));
        service.Raise(new NotificationEvent(NotificationEventKind.SyncSucceeded, "s-1"));

        presenter.Presented.Select(notification => notification.Category).ShouldBe([NotificationCategory.Informational]);
        service.Active.Count.ShouldBe(1);
    }

    [Fact]
    public void notify_conflicts_and_errors_but_not_completion_by_default()
    {
        var (service, presenter, _, _, _) = Create();

        service.Raise(new NotificationEvent(NotificationEventKind.ConflictDetected, "c-1"));
        service.Raise(new NotificationEvent(NotificationEventKind.ItemFailed, "f-1"));
        service.Raise(new NotificationEvent(NotificationEventKind.SyncSucceeded, "s-1"));

        presenter.Presented.Count.ShouldBe(2);
    }

    [Fact]
    public void carry_a_deep_link_for_a_conflict()
    {
        var (service, presenter, _, shell, navigation) = Create();
        service.Raise(new NotificationEvent(NotificationEventKind.ConflictDetected, "c-7"));

        var notification = presenter.Presented.Single();
        var result = service.Open(notification);

        notification.Category.ShouldBe(NotificationCategory.Conflict);
        notification.Target.ShouldBe(new NotificationTarget(ScreenId.Conflicts, "c-7"));
        result.Value.ShouldBeOfType<Ok<Unit>>();
        shell.Shown.ShouldBe(1);
        navigation.Current.Id.ShouldBe(ScreenId.Conflicts);
        navigation.CurrentTargetId.ShouldBe("c-7");
    }

    [Theory]
    [MemberData(nameof(NotifyingKinds))]
    public void state_what_happened_and_what_to_do_next(string kindName)
    {
        var (service, presenter, _, _, _) = Create();

        service.Raise(new NotificationEvent(Enum.Parse<NotificationEventKind>(kindName), "x-1"));

        var notification = presenter.Presented.Single();
        new[] { notification.Title, notification.Message, notification.NextStep }.ShouldAllBe(text => !string.IsNullOrWhiteSpace(text) && !text.StartsWith('!'));
    }

    [Fact]
    public void cover_every_category()
    {
        var (service, presenter, _, _, _) = Create();

        foreach (var kind in Enum.GetValues<NotificationEventKind>()) service.Raise(new NotificationEvent(kind, "x"));

        presenter.Presented.Select(notification => notification.Category).Distinct().Count().ShouldBe(Enum.GetValues<NotificationCategory>().Length);
    }

    [Fact]
    public void show_one_localised_sample_per_category()
    {
        var samples = SampleNotifications.Create(RealLocalisation());

        samples.Select(sample => sample.Category).ShouldBe(Enum.GetValues<NotificationCategory>());
        samples.ShouldAllBe(sample => !sample.Title.StartsWith('!') && !sample.Message.StartsWith('!') && !sample.NextStep.StartsWith('!'));
    }

    [Fact]
    public void keep_what_the_placeholder_presenter_was_given()
    {
        var presenter = new PlaceholderNotificationPresenter();
        var sample = SampleNotifications.Create(RealLocalisation())[0];

        presenter.Present(sample);

        presenter.Presented.ShouldBe([sample]);
    }
}
