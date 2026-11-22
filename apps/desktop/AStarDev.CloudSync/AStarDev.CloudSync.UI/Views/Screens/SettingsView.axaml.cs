using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views.Screens;

/// <summary>Settings screen: a category list with the General, Providers, Synchronisation, Appearance, Notifications and Advanced sections.</summary>
public partial class SettingsView : UserControl
{
    /// <summary>Builds the controls declared in <c>SettingsView.axaml</c>.</summary>
    public SettingsView()
    {
        InitializeComponent();
    }
}
