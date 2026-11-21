using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views;

/// <summary>The remote folder tree with a checkbox per folder. Its data context is a <c>FolderTreeViewModel</c>; Settings and onboarding both show it.</summary>
public partial class FolderTreeView : UserControl
{
    /// <summary>Builds the controls declared in <c>FolderTreeView.axaml</c>.</summary>
    public FolderTreeView()
    {
        InitializeComponent();
    }
}
