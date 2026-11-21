using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal static class OperationScreensHost
{
    public static IHost Create()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();

        return builder.Build();
    }

    public static T Screen<T>(this IHost host) => host.Services.GetServices<Navigation.IScreen>().OfType<T>().Single();
}
