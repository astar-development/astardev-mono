using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views.Screens;

/// <summary>Shows the Queue screen: status counts, a note that conflicts are resolved elsewhere, and the shared operation list. <c>ViewLocator</c> creates it for <c>QueueViewModel</c>.</summary>
public partial class QueueView : UserControl
{
    /// <summary>Builds the controls declared in <c>QueueView.axaml</c>.</summary>
    public QueueView()
    {
        InitializeComponent();
    }
}
