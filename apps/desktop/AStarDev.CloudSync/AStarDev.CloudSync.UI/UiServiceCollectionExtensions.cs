using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Observability;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.Notifications;
using AStarDev.CloudSync.UI.Theming;
using AStarDev.CloudSync.UI.Tray;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.ViewModels.Onboarding;
using AStarDev.FunctionalParadigm;
using AStarDev.SingleInstance;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.UI;

/// <summary>The UI's whole service graph in one place, so the application and the tests compose exactly the same services.</summary>
internal static class UiServiceCollectionExtensions
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan NotificationAggregationWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Registers telemetry, so the UI exports its IPC spans, metrics and logs to the <c>OpenTelemetry:OtlpEndpoint</c> setting the daemon uses. The UI
    /// span of a request and the daemon span it caused then share one trace. An empty endpoint exports nothing, and a malformed one fails the host when it starts.
    /// </summary>
    /// <remarks>
    /// Every class implementing <see cref="IScreen"/> in this assembly is registered as a singleton screen, so a slice adds or replaces a screen without editing this method.
    /// The daemon connection, folder picker owner, navigation, status bar, main view model and tray lifecycle are registered here too.
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = Host.CreateApplicationBuilder();
    /// builder.Services.AddCloudSyncUi();
    /// await builder.Build().StartAsync(cancellationToken);
    /// </code>
    /// </example>
    public static IServiceCollection AddCloudSyncUi(this IServiceCollection services)
    {
        services.AddCloudSyncTelemetry(ApplicationConstants.ServiceName);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleInstance();
        services.AddSingleton(_ => SingleInstanceOptionsFactory.Create(DaemonSocketPath.InstanceFolder(Environment.GetEnvironmentVariable(ApplicationConstants.XdgRuntimeDirEnvironmentVariable)), ApplicationConstants.InstanceName, null));
        services.AddSingleton<InstanceHold>();
        services.AddSingleton<ICultureService>(new CultureService(LocalisationService.DefaultLanguage));
        services.AddSingleton<ILocalisationService>(provider => CreateLocalisation(provider.GetRequiredService<ICultureService>()));
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<Func<IIpcClient>>(_ =>
        {
            var socketPath = DaemonSocketPath.Resolve(Environment.GetEnvironmentVariable(ApplicationConstants.XdgRuntimeDirEnvironmentVariable));

            return () => UnixSocketIpc.CreateClient(socketPath);
        });
        services.AddSingleton(provider => new ConnectionViewModel(provider.GetRequiredService<Func<IIpcClient>>(), InitialRetryDelay, MaxRetryDelay, action => Dispatcher.UIThread.Post(action)));
        services.AddSingleton<IDashboardSource>(provider => new IpcDashboardSource(provider.GetRequiredService<Func<IIpcClient>>()));
        services.AddSingleton(provider => new ConnectViewModel(provider.GetRequiredService<Func<IIpcClient>>()));
        services.AddSingleton(provider => new AccountsViewModel(provider.GetRequiredService<Func<IIpcClient>>()));
        services.AddSingleton<AvaloniaShell>();
        services.AddSingleton<IMainWindowHost>(provider => provider.GetRequiredService<AvaloniaShell>());
        services.AddSingleton<IApplicationExit>(provider => provider.GetRequiredService<AvaloniaShell>());
        services.AddSingleton<IFolderPicker, AvaloniaFolderPicker>();
        services.AddSingleton<IFilePicker, AvaloniaFilePicker>();
        services.AddSingleton<IUriLauncher, AvaloniaUriLauncher>();
        services.AddSingleton<ExportCommand>();
        services.AddSingleton(provider => new ScopeSelectionViewModel(provider.GetRequiredService<Func<IIpcClient>>(), provider.GetRequiredService<IFolderPicker>()));
        services.AddSingleton(provider => new FolderTreeViewModel(provider.GetRequiredService<Func<IIpcClient>>(), provider.GetRequiredService<ScopeSelectionViewModel>()));
        services.AddSingleton<RefreshCoordinator>();
        services.AddSingleton<Func<INavigationService>>(provider => provider.GetRequiredService<INavigationService>);
        services.AddSingleton<Func<INotificationService>>(provider => provider.GetRequiredService<INotificationService>);
        foreach (var screen in typeof(App).Assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IScreen).IsAssignableFrom(type))) services.AddSingleton(typeof(IScreen), screen);
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<SettingsStartup>();
        services.AddSingleton(provider => new StatusBarViewModel(provider.GetRequiredService<ConnectionViewModel>(), provider.GetRequiredService<ILocalisationService>(), provider.GetRequiredService<ICultureService>()));
        services.AddSingleton(provider => new OnboardingViewModel(provider.GetRequiredService<ILocalisationService>(), provider.GetRequiredService<ICultureService>(), provider.GetRequiredService<Func<IIpcClient>>(), provider.GetRequiredService<Func<INavigationService>>(), provider.GetRequiredService<ConnectionViewModel>(), provider.GetRequiredService<AccountsViewModel>(), provider.GetRequiredService<ConnectViewModel>(), provider.GetRequiredService<FolderTreeViewModel>(), provider.GetRequiredService<ScopeSelectionViewModel>()));
        services.AddSingleton(provider => new MainViewModel(provider.GetRequiredService<INavigationService>(), provider.GetServices<IScreen>(), provider.GetRequiredService<StatusBarViewModel>(), provider.GetRequiredService<OnboardingViewModel>()));
        services.AddSingleton<TrayLifecycle>();
        services.AddSingleton<PlaceholderNotificationPresenter>();
        services.AddSingleton<IDesktopNotifier, NotifySendNotifier>();
        services.AddSingleton<INotificationPresenter, DesktopNotificationPresenter>();
        services.AddSingleton<INotificationService>(provider => new NotificationService(provider.GetRequiredService<INotificationPresenter>(), provider.GetRequiredService<TrayLifecycle>(), provider.GetRequiredService<ILocalisationService>(), TimeProvider.System, NotificationAggregationWindow));
        services.AddSingleton<DaemonNotificationBridge>();
        services.AddSingleton<TrayConflictBadge>();

        return services;
    }

    private static LocalisationService CreateLocalisation(ICultureService culture) => LocalisationCatalogue.Load(typeof(App).Assembly, LocalisationService.DefaultLanguage) switch
    {
        Ok<IReadOnlyDictionary<string, string>> ok => new LocalisationService(culture, new Dictionary<string, IReadOnlyDictionary<string, string>> { [LocalisationService.DefaultLanguage] = ok.Value }, LocalisationService.DefaultLanguage),
        Fail<LocalisationError> fail => throw new InvalidOperationException($"The embedded localisation resources are invalid: {fail.Error.Value}").WithHelpLink(UiErrorCodes.LocalisationResourcesInvalid)
    };
}
