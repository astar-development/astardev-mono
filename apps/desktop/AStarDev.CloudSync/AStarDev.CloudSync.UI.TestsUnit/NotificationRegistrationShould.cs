using AStarDev.CloudSync.UI.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class NotificationRegistrationShould
{
    [Fact]
    public void resolve_the_notification_service_with_the_placeholder_presenter_from_the_real_composition()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();
        using var host = builder.Build();

        var service = host.Services.GetRequiredService<INotificationService>();
        service.Raise(new NotificationEvent(NotificationEventKind.ConflictDetected, "c-1"));

        host.Services.GetRequiredService<PlaceholderNotificationPresenter>().Presented.Single().Target.TargetId.ShouldBe("c-1");
    }
}
