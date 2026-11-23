using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views.Screens;

/// <summary>Shows the History screen: the shared operation list. <c>ViewLocator</c> creates it for <c>HistoryViewModel</c>.</summary>
public partial class HistoryView : UserControl
{
    /// <summary>Builds the controls declared in <c>HistoryView.axaml</c>.</summary>
    public HistoryView()
    {
        InitializeComponent();
    }
}
