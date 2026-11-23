using System.Xml.Linq;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class NoPlaceholderTextShould
{
    private static readonly string[] Forbidden = ["placeholder", "sample", "not yet available"];

    private static bool IsPlaceholderText(string text) => Forbidden.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> ViewTexts(string path)
        => XDocument.Load(path).Descendants().SelectMany(element => element.Attributes().Select(attribute => attribute.Value).Concat(element.Nodes().OfType<XText>().Select(text => text.Value)));

    [Fact]
    public void not_appear_in_any_localisation_value()
    {
        var catalogue = LocalisationCatalogue.Load(typeof(App).Assembly, LocalisationService.DefaultLanguage).Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        catalogue.Where(entry => IsPlaceholderText(entry.Value)).Select(entry => entry.Key).ShouldBeEmpty();
    }

    [Fact]
    public void not_appear_in_any_view()
    {
        var views = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Views"), "*.axaml", SearchOption.AllDirectories);

        views.ShouldNotBeEmpty();
        views.Where(view => ViewTexts(view).Any(IsPlaceholderText)).Select(view => Path.GetFileName(view)).ShouldBeEmpty();
    }
}
