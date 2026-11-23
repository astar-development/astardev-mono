using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Notifications;
using AStarDev.CloudSync.UI.Theming;
using AStarDev.CloudSync.UI.Tray;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.Views;
using AStarDev.FunctionalParadigm;
using AStarDev.SingleInstance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.UI;

/// <summary>Avalonia application root: loads <c>App.axaml</c>, starts the daemon connection and opens the main window when run as a desktop app.</summary>
public partial class App : Application
{
    private const string DefaultTheme = "Dark";

    /// <summary>Loads the styles and resources declared in <c>App.axaml</c>.</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Claims the single running instance first. The running instance starts the <see cref="ConnectionViewModel"/> retry loop against the daemon socket and opens the <see cref="MainWindow"/> bound to it;
    /// a second launch asks it to show its window and exits, and a launch that cannot enforce a single instance exits with a failure code.
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
            var shutdown = new CancellationTokenSource();
            desktop.Exit += (_, _) =>
            {
                shutdown.Cancel();
                host.Dispose();
            };
            _ = ClaimAsync(host, desktop, shutdown.Token);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task ClaimAsync(IHost host, IClassicDesktopStyleApplicationLifetime desktop, CancellationToken cancellationToken)
    {
        var claim = await host.Services.GetRequiredService<ISingleInstanceGuard>().ClaimAsync(host.Services.GetRequiredService<SingleInstanceOptions>(), cancellationToken);
        _ = InstanceStartup.ExitCodeFor(claim).Match(code => ExitWith(desktop, code), () => StartRunningInstance(host, desktop, claim, cancellationToken));
    }

    private static Unit ExitWith(IClassicDesktopStyleApplicationLifetime desktop, int exitCode)
    {
        desktop.Shutdown(exitCode);

        return default;
    }

    private Unit StartRunningInstance(IHost host, IClassicDesktopStyleApplicationLifetime desktop, InstanceClaim claim, CancellationToken cancellationToken)
    {
        if (claim is not InstanceClaim.Primary primary)
            return default;

        host.Services.GetRequiredService<InstanceHold>().Keep(primary.Instance);
        var localisation = host.Services.GetRequiredService<ILocalisationService>();
        LocalisationHost.Service = localisation;
        var connection = host.Services.GetRequiredService<ConnectionViewModel>();
        _ = host.Services.GetRequiredService<RefreshCoordinator>();
        if (host.Services.GetRequiredService<IThemeService>().Apply(DefaultTheme).Value is Fail<ThemeError> themeFailure)
            UiLog.ThemeFailed(host.Services.GetRequiredService<ILogger<App>>(), DefaultTheme, themeFailure.Error.Value.GetType().Name, ErrorInfo.CodeOf(themeFailure.Error.Value));
        _ = host.Services.GetRequiredService<SettingsStartup>();
        _ = RunAsync(host, connection, cancellationToken);

        var window = new MainWindow { DataContext = host.Services.GetRequiredService<MainViewModel>() };
        var tray = host.Services.GetRequiredService<TrayLifecycle>();
        _ = host.Services.GetRequiredService<TrayConflictBadge>();
        _ = host.Services.GetRequiredService<DaemonNotificationBridge>();
        var shell = host.Services.GetRequiredService<AvaloniaShell>();
        shell.Attach(window, desktop);
        primary.Instance.ActivationRequested += (_, _) => Dispatcher.UIThread.Post(shell.Show);
        window.Closing += (_, args) =>
        {
            if (args.CloseReason == WindowCloseReason.WindowClosing && tray.HandleWindowClosing()) args.Cancel = true;
        };
        TrayIcon.SetIcons(this, [TrayIconFactory.Create(tray, localisation)]);
        desktop.MainWindow = window;
        shell.Show();

        return default;
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
