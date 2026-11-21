using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class StatusIndicatorViewModelShould
{
    public static TheoryData<TrustLevel> Levels => new(Enum.GetValues<TrustLevel>());

    private static LocalisationService RealLocalisation()
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(new CultureService("en-GB"), new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void always_expose_text_and_icon_with_colour(TrustLevel level)
    {
        var indicator = StatusIndicatorViewModel.For(level, RealLocalisation());

        indicator.Text.ShouldNotBeNullOrWhiteSpace();
        indicator.Text.ShouldNotStartWith("!");
        indicator.IconKey.ShouldBe($"Icon.Trust.{level}");
        indicator.ColourKey.ShouldBe($"Trust.{level}.Brush");
    }

    [Fact]
    public void use_the_level_name_when_given_blank_text() => new StatusIndicatorViewModel(TrustLevel.Degraded, " ").Text.ShouldBe("Degraded");
}
