using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views;

/// <summary>Shows a placeholder screen: its title and "not yet available" text. <see cref="ViewLocator"/> falls back to it for any stub that has no view of its own.</summary>
public partial class StubScreenView : UserControl
{
    /// <summary>Builds the controls declared in <c>StubScreenView.axaml</c>.</summary>
    public StubScreenView()
    {
        InitializeComponent();
    }
}
