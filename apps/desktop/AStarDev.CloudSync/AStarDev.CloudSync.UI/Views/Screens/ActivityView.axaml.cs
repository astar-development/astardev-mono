using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views.Screens;

/// <summary>Shows the Activity screen: its title and the shared operation list. <c>ViewLocator</c> creates it for <c>ActivityViewModel</c>.</summary>
public partial class ActivityView : UserControl
{
    /// <summary>Builds the controls declared in <c>ActivityView.axaml</c>.</summary>
    public ActivityView()
    {
        InitializeComponent();
    }
}
