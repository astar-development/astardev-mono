using AStarDev.CloudSync.UI.ViewModels.Providers;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Path = Avalonia.Controls.Shapes.Path;

namespace AStarDev.CloudSync.UI.Views.Providers;

/// <summary>Draws a <see cref="ProviderHealthViewModel"/>: its icon and text in the colour the active theme gives that health state.</summary>
public partial class ProviderHealthView : UserControl
{
    /// <summary>Builds the controls declared in <c>ProviderHealthView.axaml</c>. Parameterless so the designer and the view locator can create it.</summary>
    public ProviderHealthView()
    {
        InitializeComponent();
    }

    /// <summary>Binds the icon and text to the theme resources named by the new view model, so a later theme change recolours them. Another data context leaves them unbound.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not ProviderHealthViewModel health) return;

        var icon = this.FindControl<Path>("Icon")!;
        var label = this.FindControl<TextBlock>("Label")!;
        icon.Bind(Path.DataProperty, new DynamicResourceExtension(health.IconKey));
        icon.Bind(Shape.FillProperty, new DynamicResourceExtension(health.ColourKey));
        label.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(health.ColourKey));
    }
}
