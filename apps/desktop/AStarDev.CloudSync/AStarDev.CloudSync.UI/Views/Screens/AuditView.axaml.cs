using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views.Screens;

/// <summary>Shows the Audit screen: the recorded entries. <c>ViewLocator</c> creates it for <c>AuditViewModel</c>.</summary>
public partial class AuditView : UserControl
{
    /// <summary>Builds the controls declared in <c>AuditView.axaml</c>.</summary>
    public AuditView()
    {
        InitializeComponent();
    }
}
