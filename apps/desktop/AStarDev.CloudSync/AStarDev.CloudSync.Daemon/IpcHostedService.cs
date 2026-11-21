using AStarDev.CloudSync.Daemon.Startup;
using AStarDev.CloudSync.IPC;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Serves the IPC contract. Registered after <see cref="StartupHostedService"/>, so it only listens once every recovery stage has finished.</summary>
internal sealed class IpcHostedService(IIpcServer server, TimeProvider timeProvider) : IHostedService
{
    private static readonly string Version = typeof(IpcHostedService).Assembly.GetName().Version?.ToString() ?? ApplicationConstants.UnknownVersion;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = new DaemonStatusView(DaemonState.Running, Version, timeProvider.GetUtcNow());
        server.MapQuery((GetDaemonStatus _, CancellationToken _) => Task.FromResult<Result<DaemonStatusView, IpcError>>(new Ok<DaemonStatusView>(status)));

        var result = await server.StartAsync(cancellationToken);

        if (result is Fail<IpcError> fail) throw new StartupFailedException(new StartupError(ApplicationConstants.AcceptConnectionsStageName, fail.Error.Value?.ToString() ?? string.Empty));
    }

    public async Task StopAsync(CancellationToken cancellationToken) => _ = await server.StopAsync(cancellationToken);
}
