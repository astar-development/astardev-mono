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

        public Task<Result<Unit, DesktopNotifierError>> ShowAsync(Notification notification, CancellationToken cancellationToken)
        {
            Shown.Add(notification);
            Tokens.Add(cancellationToken);

            return Task.FromResult<Result<Unit, DesktopNotifierError>>(new Ok<Unit>(default));
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
        host.Services.GetRequiredService<PlaceholderNotificationPresenter>().Presented.Count.ShouldBe(1);
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
    public void follow_conflicts_with_the_tray_badge_in_the_composed_app()
    {
        var (host, _) = CreateHost();
        using var disposable = host;
        _ = host.Services.GetRequiredService<TrayConflictBadge>();

        host.Services.GetRequiredService<ConnectionViewModel>().Conflicts.Add(new IPC.ConflictView(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ModifyModify", DateTimeOffset.UnixEpoch, "Open", "a.txt", null, null));

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
