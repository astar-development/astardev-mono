using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Tray;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class TrayStateShould
{
    public static TheoryData<TrustLevel> Levels => new(Enum.GetValues<TrustLevel>());

    private static LocalisationService RealLocalisation()
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(new CultureService("en-GB"), new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void expose_text_and_icon_for_every_state(TrustLevel level)
    {
        var state = TrayState.For(level, RealLocalisation());

        state.Level.ShouldBe(level);
        state.ToolTip.ShouldNotBeNullOrWhiteSpace();
        state.ToolTip.ShouldNotStartWith("!");
        state.IconKey.ShouldBe($"Icon.Trust.{level}");
    }

    [Fact]
    public void use_a_different_tooltip_for_every_state()
    {
        var localisation = RealLocalisation();

        var tooltips = Enum.GetValues<TrustLevel>().Select(level => TrayState.For(level, localisation).ToolTip);

        tooltips.Distinct().Count().ShouldBe(Enum.GetValues<TrustLevel>().Length);
    }

    [Fact]
    public void draw_a_different_icon_shape_for_every_state()
    {
        var icons = Enum.GetValues<TrustLevel>().Select(level => Convert.ToBase64String(TrayIconPixels.Render(level))).Distinct();

        icons.Count().ShouldBe(Enum.GetValues<TrustLevel>().Length);
    }

    [Fact]
    public void draw_an_opaque_glyph_on_a_transparent_background()
    {
        var pixels = TrayIconPixels.Render(TrustLevel.Healthy);

        pixels.Length.ShouldBe(TrayIconPixels.Size * TrayIconPixels.Size * 4);
        pixels[3].ShouldBe((byte)0);
        pixels.Where((_, index) => index % 4 == 3).ShouldContain((byte)255);
    }
}
