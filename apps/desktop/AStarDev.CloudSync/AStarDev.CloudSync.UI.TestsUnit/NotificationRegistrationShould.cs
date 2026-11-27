using AStarDev.CloudSync.UI.Notifications;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class NotificationRegistrationShould
{
    private sealed class SilentNotifier : IDesktopNotifier
    {
        public int Calls { get; private set; }

        public Task<Result<bool, DesktopNotifierError>> ShowAsync(Notification notification, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult<Result<bool, DesktopNotifierError>>(new Ok<bool>(false));
        }
    }

    [Fact]
    public void resolve_the_notification_service_with_the_in_app_presenter_from_the_real_composition()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();
        var notifier = new SilentNotifier();
        builder.Services.Replace(ServiceDescriptor.Singleton<IDesktopNotifier>(notifier));
        using var host = builder.Build();

        var service = host.Services.GetRequiredService<INotificationService>();
        service.Raise(new NotificationEvent(NotificationEventKind.ConflictDetected, "c-1"));

        host.Services.GetRequiredService<InAppNotificationPresenter>().Presented.Single().Target.TargetId.ShouldBe("c-1");
        notifier.Calls.ShouldBe(1);
    }
}
