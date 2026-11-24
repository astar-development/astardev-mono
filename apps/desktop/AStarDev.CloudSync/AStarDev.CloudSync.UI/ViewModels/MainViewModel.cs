using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Onboarding;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>State behind <see cref="Views.MainWindow"/>: the navigation rail, the screen in the content area and the status bar.</summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly INavigationService navigation;

    /// <summary>Creates the view model and follows the navigation service.</summary>
    /// <param name="navigation">Decides the content. Navigating from anywhere updates <see cref="Content"/> and <see cref="SelectedDestination"/>.</param>
    /// <param name="screens">Every registered screen. The primary ones become <see cref="Destinations"/>.</param>
    /// <param name="statusBar">The permanent status bar.</param>
    /// <param name="onboarding">The first-run flow, drawn over the window while it is active.</param>
    internal MainViewModel(INavigationService navigation, IEnumerable<IScreen> screens, StatusBarViewModel statusBar, OnboardingViewModel onboarding)
    {
        this.navigation = navigation;
        StatusBar = statusBar;
        Onboarding = onboarding;
        var byId = screens.ToDictionary(screen => screen.Id);
        Destinations = ScreenIds.Primary.Select(id => new DestinationViewModel(id, byId[id].Title, byId[id].Glyph)).ToList();
        BackCommand = new RelayCommand(() => _ = navigation.Navigate(ScreenId.Settings));
        navigation.Navigated += (_, _) =>
        {
            OnPropertyChanged(nameof(Content));
            OnPropertyChanged(nameof(IsAdvancedScreen));
            OnPropertyChanged(nameof(SelectedDestination));
        };
    }

    /// <summary>The rail entries in display order: Dashboard, Queue, Conflicts, Activity, History, Providers, Settings.</summary>
    public IReadOnlyList<DestinationViewModel> Destinations { get; }

    /// <summary>The screen shown in the content area.</summary>
    public IScreen Content => navigation.Current;

    /// <summary>The highlighted rail entry: the current screen, or Settings while an advanced screen is shown. Setting it navigates; <see langword="null"/> is ignored.</summary>
    public DestinationViewModel? SelectedDestination
    {
        get => Destinations.FirstOrDefault(destination => destination.Id == ScreenIds.RailEntryOf(navigation.Current.Id));
        set
        {
            if (value is not null) _ = navigation.Navigate(value.Id);
        }
    }

    /// <summary>True while Metrics, Logs, Traces or Diagnostics is shown, so the window offers a way back to Settings.</summary>
    public bool IsAdvancedScreen => navigation.Current.Id != ScreenIds.RailEntryOf(navigation.Current.Id);

    /// <summary>Returns from an advanced screen to Settings.</summary>
    public IRelayCommand BackCommand { get; }

    /// <summary>The permanent status bar.</summary>
    public StatusBarViewModel StatusBar { get; }

    internal OnboardingViewModel Onboarding { get; }
}
