using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.Views;

namespace AStarDev.CloudSync.UI;

public partial class App : Application
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var socketPath = DaemonSocketPath.Resolve(Environment.GetEnvironmentVariable(ApplicationConstants.XdgRuntimeDirEnvironmentVariable));
            var connection = new ConnectionViewModel(() => UnixSocketIpc.CreateClient(socketPath), InitialRetryDelay, MaxRetryDelay, action => Dispatcher.UIThread.Post(action));
            var shutdown = new CancellationTokenSource();
            desktop.Exit += (_, _) => shutdown.Cancel();
            _ = connection.RunAsync(shutdown.Token);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(connection),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
