using AStarDev.CloudSync.UI.ViewModels.Operations;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AStarDev.CloudSync.UI.Views;

/// <summary>Draws an <see cref="OperationListViewModel"/>: filters, the operation list, the details panel, and the export. Shared by Queue, Activity and History.</summary>
public partial class OperationListView : UserControl
{
    /// <summary>Builds the controls declared in <c>OperationListView.axaml</c>.</summary>
    public OperationListView()
    {
        InitializeComponent();
    }

    private void OnCloseDetails(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperationListViewModel list) list.Details.Close();
    }
}
