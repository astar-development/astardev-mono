using Avalonia.Markup.Xaml;

namespace AStarDev.CloudSync.UI.Localisation;

/// <summary>XAML markup extension giving the localised text for a key: <c>Text="{loc:Loc Common.Connect}"</c>.</summary>
/// <param name="key">The resource key, for example <c>Common.Connect</c>.</param>
public sealed class LocExtension(string key) : MarkupExtension
{
    /// <summary>The text for the key, read once when the view loads. Without a service yet (designer, tests) it is the key itself.</summary>
    public override object ProvideValue(IServiceProvider serviceProvider) => LocalisationHost.Service?.Get(key) ?? key;
}
