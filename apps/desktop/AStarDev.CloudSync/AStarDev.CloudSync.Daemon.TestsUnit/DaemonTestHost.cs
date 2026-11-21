using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

internal sealed class DaemonTestHost(IHost host, string socketPath) : IAsyncDisposable
{
    public string SocketPath => socketPath;

    public IServiceProvider Services => host.Services;

    public static DaemonTestHost Create()
    {
        var socketPath = Path.Combine(Path.GetTempPath(), $"cloudsync-daemon-{Guid.NewGuid():N}.sock");
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[$"{ApplicationConstants.IpcConfigurationSection}:{nameof(IpcOptions.SocketPath)}"] = socketPath;
        builder.Services.AddCloudSyncDaemon();

        return new DaemonTestHost(builder.Build(), socketPath);
    }

    public Task StartAsync(CancellationToken cancellationToken) => host.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => host.StopAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        host.Dispose();
    }
}
