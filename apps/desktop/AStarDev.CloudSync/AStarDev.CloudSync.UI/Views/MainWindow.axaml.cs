using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views;

/// <summary>Top-level desktop window. Layout lives in <c>MainWindow.axaml</c>; <see cref="App"/> sets its data context.</summary>
public partial class MainWindow : Window
{
    /// <summary>Builds the controls declared in <c>MainWindow.axaml</c>. Parameterless so the Avalonia designer can create it.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}