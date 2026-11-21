using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.Views;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI;

/// <summary>Avalonia application root: loads <c>App.axaml</c>, starts the daemon connection and opens the main window when run as a desktop app.</summary>
public partial class App : Application
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>Loads the styles and resources declared in <c>App.axaml</c>.</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Starts the <see cref="ConnectionViewModel"/> retry loop against the daemon socket and opens the <see cref="MainWindow"/> bound to it.
    /// The loop is cancelled when the desktop lifetime exits. Other lifetimes get no window and no connection.
    /// Telemetry starts before the first connection attempt and is flushed on exit; when it cannot start the UI still connects, just without exporting.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var socketPath = DaemonSocketPath.Resolve(Environment.GetEnvironmentVariable(ApplicationConstants.XdgRuntimeDirEnvironmentVariable));
            var connection = new ConnectionViewModel(() => UnixSocketIpc.CreateClient(socketPath), InitialRetryDelay, MaxRetryDelay, action => Dispatcher.UIThread.Post(action));
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddCloudSyncUi();
            var host = builder.Build();
            var shutdown = new CancellationTokenSource();
            desktop.Exit += (_, _) =>
            {
                shutdown.Cancel();
                host.Dispose();
            };
            _ = RunAsync(host, connection, shutdown.Token);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(connection),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task RunAsync(IHost host, ConnectionViewModel connection, CancellationToken cancellationToken)
    {
        _ = await Try.RunAsync(async token =>
        {
            await host.StartAsync(token);
            return default(Unit);
        }, cancellationToken);

        await connection.RunAsync(cancellationToken);
    }
}
