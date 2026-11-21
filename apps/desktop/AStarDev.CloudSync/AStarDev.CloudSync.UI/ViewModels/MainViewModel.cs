using AStarDev.CloudSync.UI.Navigation;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>State behind <see cref="Views.MainWindow"/>: the navigation rail, the screen in the content area and the status bar.</summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly INavigationService navigation;

    /// <summary>Creates the view model and follows the navigation service.</summary>
    /// <param name="navigation">Decides the content. Navigating from anywhere updates <see cref="Content"/> and <see cref="SelectedDestination"/>.</param>
    /// <param name="screens">Every registered screen. The primary ones become <see cref="Destinations"/>.</param>
    /// <param name="statusBar">The permanent status bar.</param>
    internal MainViewModel(INavigationService navigation, IEnumerable<IScreen> screens, StatusBarViewModel statusBar)
    {
        this.navigation = navigation;
        StatusBar = statusBar;
        var byId = screens.ToDictionary(screen => screen.Id);
        Destinations = ScreenIds.Primary.Select(id => new DestinationViewModel(id, byId[id].Title, byId[id].Glyph)).ToList();
        navigation.Navigated += (_, _) =>
        {
            OnPropertyChanged(nameof(Content));
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

    /// <summary>The permanent status bar.</summary>
    public StatusBarViewModel StatusBar { get; }
}
