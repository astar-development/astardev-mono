using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.Views;

namespace AStarDev.CloudSync.UI;

/// <summary>Avalonia application root: loads <c>App.axaml</c> and opens the main window when run as a desktop app.</summary>
public partial class App : Application
{
    /// <summary>Loads the styles and resources declared in <c>App.axaml</c>.</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Opens the <see cref="MainWindow"/> with a new <see cref="MainViewModel"/> as its data context. Other lifetimes get no window.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}