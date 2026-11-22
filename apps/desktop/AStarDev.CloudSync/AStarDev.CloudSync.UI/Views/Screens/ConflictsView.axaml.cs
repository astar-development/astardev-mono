using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AStarDev.CloudSync.UI.Views.Screens;

/// <summary>Conflicts screen: list, detail comparison, bulk selection and the resolution buttons.</summary>
public partial class ConflictsView : UserControl
{
    /// <summary>Builds the controls declared in <c>ConflictsView.axaml</c>.</summary>
    public ConflictsView()
    {
        InitializeComponent();
    }

    private ConflictsViewModel? Model => DataContext as ConflictsViewModel;

    private void OnSelectAll(object? sender, RoutedEventArgs args) => Model?.SelectAll();

    private void OnClearSelection(object? sender, RoutedEventArgs args) => Model?.ClearSelection();

    private void OnCancel(object? sender, RoutedEventArgs args) => Model?.CancelResolution();

    private void OnAction(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: ConflictChoice choice }) Model?.RequestResolution(choice);
    }
}
