using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class CultureServiceShould
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 21, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void format_dates_for_the_active_culture()
    {
        var british = new CultureService("en-GB");
        var american = new CultureService("en-US");

        british.FormatDate(Moment).ShouldBe("21/11/2026");
        american.FormatDate(Moment).ShouldBe("11/21/2026");
    }

    [Fact]
    public void format_times_for_the_active_culture() => new CultureService("en-GB").FormatTime(Moment).ShouldBe("15:30");

    [Fact]
    public void format_numbers_with_the_group_separator_of_the_active_culture()
    {
        new CultureService("en-GB").FormatNumber(1234567).ShouldBe("1,234,567");
        new CultureService("es-ES").FormatNumber(1234567).ShouldBe("1.234.567");
    }
}
