using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views.Screens;

/// <summary>Shows the Logs screen: the filters and the stored entries. <c>ViewLocator</c> creates it for <c>LogsViewModel</c>.</summary>
public partial class LogsView : UserControl
{
    /// <summary>Builds the controls declared in <c>LogsView.axaml</c>.</summary>
    public LogsView()
    {
        InitializeComponent();
    }
}
