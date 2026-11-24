using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views.Screens;

/// <summary>Shows the Classifications screen: the terms with their filters and the files of the selected term. <c>ViewLocator</c> creates it for <c>ClassificationsViewModel</c>.</summary>
public partial class ClassificationsView : UserControl
{
    /// <summary>Builds the controls declared in <c>ClassificationsView.axaml</c>.</summary>
    public ClassificationsView()
    {
        InitializeComponent();
    }
}
