using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views.Screens;

/// <summary>Shows the Traces screen: the filters and the stored spans. <c>ViewLocator</c> creates it for <c>TracesViewModel</c>.</summary>
public partial class TracesView : UserControl
{
    /// <summary>Builds the controls declared in <c>TracesView.axaml</c>.</summary>
    public TracesView()
    {
        InitializeComponent();
    }
}
