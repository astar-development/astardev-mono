using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views.Screens;

/// <summary>Shows the Metrics screen: each instrument's current value and recent history. <c>ViewLocator</c> creates it for <c>MetricsViewModel</c>.</summary>
public partial class MetricsView : UserControl
{
    /// <summary>Builds the controls declared in <c>MetricsView.axaml</c>.</summary>
    public MetricsView()
    {
        InitializeComponent();
    }
}
