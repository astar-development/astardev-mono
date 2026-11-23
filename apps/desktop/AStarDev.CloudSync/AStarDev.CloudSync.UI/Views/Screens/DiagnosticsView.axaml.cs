using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views.Screens;

/// <summary>Shows the Diagnostics screen: daemon version, state, uptime and the health checks. <c>ViewLocator</c> creates it for <c>DiagnosticsViewModel</c>.</summary>
public partial class DiagnosticsView : UserControl
{
    /// <summary>Builds the controls declared in <c>DiagnosticsView.axaml</c>.</summary>
    public DiagnosticsView()
    {
        InitializeComponent();
    }
}
