using AStarDev.CloudSync.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace AStarDev.CloudSync.UI.Views;

/// <summary>Draws a <see cref="StatusIndicatorViewModel"/>: an icon and its text in the colour the active theme gives that trust level.</summary>
public partial class StatusIndicatorView : UserControl
{
    /// <summary>Builds the controls declared in <c>StatusIndicatorView.axaml</c>. Parameterless so the Avalonia designer and <see cref="ViewLocator"/> can create it.</summary>
    public StatusIndicatorView()
    {
        InitializeComponent();
    }

    /// <summary>Binds the icon and text to the theme resources named by the new view model, so a later theme change recolours them. Another data context leaves them unbound.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not StatusIndicatorViewModel indicator) return;

        var icon = this.FindControl<Path>("Icon")!;
        var label = this.FindControl<TextBlock>("Label")!;
        icon.Bind(Path.DataProperty, new DynamicResourceExtension(indicator.IconKey));
        icon.Bind(Shape.FillProperty, new DynamicResourceExtension(indicator.ColourKey));
        label.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(indicator.ColourKey));
    }
}
