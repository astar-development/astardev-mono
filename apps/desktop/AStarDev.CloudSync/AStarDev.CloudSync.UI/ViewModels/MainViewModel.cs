using CommunityToolkit.Mvvm.ComponentModel;

namespace AStarDev.CloudSync.UI.ViewModels;

/// <summary>State behind <see cref="Views.MainWindow"/>.</summary>
public partial class MainViewModel : ViewModelBase
{
    /// <summary>Placeholder text shown until the main window has real content.</summary>
    [ObservableProperty]
    public partial string Greeting { get; set; } = "Welcome to Avalonia!";
}
