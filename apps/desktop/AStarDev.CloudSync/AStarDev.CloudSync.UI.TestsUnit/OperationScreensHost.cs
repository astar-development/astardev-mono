using AStarDev.CloudSync.IPC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

internal static class OperationScreensHost
{
    public static IHost Create(IIpcClient? client = null, TimeProvider? time = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddCloudSyncUi();
        if (client is not null) builder.Services.AddSingleton<Func<IIpcClient>>(_ => () => client);
        if (time is not null) builder.Services.AddSingleton(time);

        return builder.Build();
    }

    public static T Screen<T>(this IHost host) => host.Services.GetServices<Navigation.IScreen>().OfType<T>().Single();
}
