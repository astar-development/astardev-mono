using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Notifications;
using AStarDev.CloudSync.UI.Theming;
using AStarDev.CloudSync.UI.Tray;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.Views;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI;

/// <summary>Avalonia application root: loads <c>App.axaml</c>, starts the daemon connection and opens the main window when run as a desktop app.</summary>
public partial class App : Application
{
    private const string DefaultTheme = "Dark";

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
            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddCloudSyncUi();
            var host = builder.Build();
            var localisation = host.Services.GetRequiredService<ILocalisationService>();
            LocalisationHost.Service = localisation;
            var connection = host.Services.GetRequiredService<ConnectionViewModel>();
            _ = host.Services.GetRequiredService<RefreshCoordinator>();
            _ = host.Services.GetRequiredService<IThemeService>().Apply(DefaultTheme);
            var shutdown = new CancellationTokenSource();
            desktop.Exit += (_, _) =>
            {
                shutdown.Cancel();
                host.Dispose();
            };
            _ = RunAsync(host, connection, shutdown.Token);

            var window = new MainWindow { DataContext = host.Services.GetRequiredService<MainViewModel>() };
            var tray = host.Services.GetRequiredService<TrayLifecycle>();
            _ = host.Services.GetRequiredService<TrayConflictBadge>();
            _ = host.Services.GetRequiredService<DaemonNotificationBridge>();
            host.Services.GetRequiredService<AvaloniaShell>().Attach(window, desktop);
            window.Closing += (_, args) =>
            {
                if (args.CloseReason == WindowCloseReason.WindowClosing && tray.HandleWindowClosing()) args.Cancel = true;
            };
            TrayIcon.SetIcons(this, [TrayIconFactory.Create(tray, localisation)]);
            desktop.MainWindow = window;
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
