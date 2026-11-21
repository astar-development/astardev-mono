using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class SyncHealthPresentationShould
{
    private static LocalisationService RealLocalisation()
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(new CultureService("en-GB"), new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    [Fact]
    public void expose_icon_and_text_for_every_state()
    {
        var localisation = RealLocalisation();

        foreach (var health in Enum.GetValues<SyncHealth>())
        {
            var presentation = SyncHealthPresentation.For(health, localisation);

            presentation.Headline.ShouldNotBeNullOrWhiteSpace();
            presentation.Headline.ShouldNotStartWith("!");
            presentation.Summary.ShouldNotBeNullOrWhiteSpace();
            presentation.Summary.ShouldNotStartWith("!");
            presentation.Indicator.IconKey.ShouldStartWith("Icon.Trust.");
            presentation.Indicator.ColourKey.ShouldStartWith("Trust.");
            presentation.Indicator.Text.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void give_each_state_its_own_icon()
    {
        var localisation = RealLocalisation();

        Enum.GetValues<SyncHealth>().Select(health => SyncHealthPresentation.For(health, localisation).Indicator.IconKey).Distinct().Count().ShouldBe(5);
    }
}
