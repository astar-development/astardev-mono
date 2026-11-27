using System.Diagnostics;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.Notifications;
using AStarDev.CloudSync.UI.Tray;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class DesktopNotificationShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeNotifier : IDesktopNotifier
    {
        public List<Notification> Shown { get; } = [];

        public List<CancellationToken> Tokens { get; } = [];

        public bool Activated { get; set; }

        public Task<Result<bool, DesktopNotifierError>> ShowAsync(Notification notification, CancellationToken cancellationToken)
        {
            Shown.Add(notification);
            Tokens.Add(cancellationToken);

            return Task.FromResult<Result<bool, DesktopNotifierError>>(new Ok<bool>(Activated));
        }
    }

    private static (IHost Host, FakeNotifier Notifier) CreateHost()
    {
        var notifier = new FakeNotifier();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();
        builder.Services.Replace(ServiceDescriptor.Singleton<IDesktopNotifier>(notifier));

        return (builder.Build(), notifier);
    }

    private static Notification Sample() => new(NotificationCategory.Conflict, "k", "Title", "Message", "Next", new NotificationTarget(ScreenId.Conflicts, null), 1);

    [Fact]
    public void show_each_event_in_the_composed_app_through_the_desktop_notifier()
    {
        var (host, notifier) = CreateHost();
        using var disposable = host;

        host.Services.GetRequiredService<INotificationService>().Raise(new NotificationEvent(NotificationEventKind.ConflictDetected, "c-1"));

        notifier.Shown.Single().Target.TargetId.ShouldBe("c-1");
        host.Services.GetRequiredService<InAppNotificationPresenter>().Presented.Count.ShouldBe(1);
    }

    [Fact]
    public void show_nothing_for_a_routine_success_in_the_composed_app()
    {
        var (host, notifier) = CreateHost();
        using var disposable = host;

        host.Services.GetRequiredService<INotificationService>().Raise(new NotificationEvent(NotificationEventKind.SyncSucceeded, "s-1"));

        notifier.Shown.ShouldBeEmpty();
    }

    [Fact]
    public void pass_the_application_stopping_token_to_the_notifier()
    {
        var (host, notifier) = CreateHost();
        using var disposable = host;

        host.Services.GetRequiredService<INotificationPresenter>().Present(Sample());

        notifier.Tokens.Single().ShouldBe(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
    }

    [Fact]
    public async Task open_the_target_when_the_desktop_notification_is_clicked()
    {
        var (host, notifier) = CreateHost();
        using var disposable = host;
        notifier.Activated = true;
        var service = new FakeNotificationService();
        var posted = new List<Action>();
        var presenter = new DesktopNotificationPresenter(host.Services.GetRequiredService<InAppNotificationPresenter>(), notifier, host.Services.GetRequiredService<IHostApplicationLifetime>(), () => service, posted.Add);

        presenter.Present(Sample());
        await Task.Delay(50, Token);
        posted.Single()();

        service.Opened.Single().Key.ShouldBe("k");
    }

    [Fact]
    public async Task not_open_anything_when_the_desktop_notification_closes_unclicked()
    {
        var (host, notifier) = CreateHost();
        using var disposable = host;
        var posted = new List<Action>();
        var presenter = new DesktopNotificationPresenter(host.Services.GetRequiredService<InAppNotificationPresenter>(), notifier, host.Services.GetRequiredService<IHostApplicationLifetime>(), () => new FakeNotificationService(), posted.Add);

        presenter.Present(Sample());
        await Task.Delay(50, Token);

        posted.ShouldBeEmpty();
    }

    [Fact]
    public void offer_a_default_open_action_on_the_notify_send_command()
    {
        var (host, _) = CreateHost();
        using var disposable = host;
        var notifier = new NotifySendNotifier(host.Services.GetRequiredService<ILocalisationService>());

        notifier.StartInfo(Sample()).ArgumentList.ShouldContain("--action=default=Open");
    }

    [Fact]
    public void follow_conflicts_with_the_tray_badge_in_the_composed_app()
    {
        var (host, _) = CreateHost();
        using var disposable = host;
        _ = host.Services.GetRequiredService<TrayConflictBadge>();

        host.Services.GetRequiredService<ConnectionViewModel>().ReplaceConflicts(ConflictPages.Of(new IPC.ConflictView(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ModifyModify", DateTimeOffset.UnixEpoch, "Open", "a.txt", null, null)));

        host.Services.GetRequiredService<TrayLifecycle>().ConflictCount.ShouldBe(1);
    }

    [Fact]
    public void resolve_the_bridge_from_the_composed_app()
    {
        var (host, _) = CreateHost();
        using var disposable = host;

        host.Services.GetRequiredService<DaemonNotificationBridge>().ShouldNotBeNull();
    }

    [Fact]
    public async Task throw_cancellation_when_the_notify_send_notifier_is_cancelled()
    {
        var (host, _) = CreateHost();
        using var disposable = host;
        var notifier = new NotifySendNotifier(host.Services.GetRequiredService<ILocalisationService>());
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => notifier.ShowAsync(Sample(), source.Token));
    }

    [Fact]
    public async Task stop_a_notify_send_that_outlives_its_timeout_and_report_it_unclicked()
    {
        var start = new ProcessStartInfo("sleep") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("60");
        var clock = Stopwatch.StartNew();

        var result = await NotifySendNotifier.RunAsync(start, TimeSpan.FromMilliseconds(200), Token);

        result.ShouldBe((0, string.Empty));
        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task throw_cancellation_and_stop_the_process_when_cancelled_while_notify_send_runs()
    {
        var start = new ProcessStartInfo("sleep") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("60");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);
        source.CancelAfter(TimeSpan.FromMilliseconds(200));
        var clock = Stopwatch.StartNew();

        await Should.ThrowAsync<OperationCanceledException>(() => NotifySendNotifier.RunAsync(start, TimeSpan.FromSeconds(30), source.Token));

        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void build_a_notify_send_command_with_title_and_body_as_separate_arguments()
    {
        var (host, _) = CreateHost();
        using var disposable = host;
        var notifier = new NotifySendNotifier(host.Services.GetRequiredService<ILocalisationService>());

        var start = notifier.StartInfo(Sample() with { Category = NotificationCategory.Critical });

        start.FileName.ShouldBe("notify-send");
        start.ArgumentList.ShouldContain("--urgency=critical");
        start.ArgumentList.ShouldContain("Title");
        start.ArgumentList.ShouldContain("Message Next");
    }

    [Fact]
    public void word_the_tooltip_with_the_conflict_count_only_when_there_are_conflicts()
    {
        var (host, _) = CreateHost();
        using var disposable = host;
        var localisation = host.Services.GetRequiredService<ILocalisationService>();

        TrayState.ToolTipFor(TrustLevel.Attention, 3, localisation).ShouldContain("3 conflicts");
        TrayState.ToolTipFor(TrustLevel.Healthy, 0, localisation).ShouldBe(TrayState.For(TrustLevel.Healthy, localisation).ToolTip);
    }
}
