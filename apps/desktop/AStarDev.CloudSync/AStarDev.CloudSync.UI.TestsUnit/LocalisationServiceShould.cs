using AStarDev.CloudSync.UI.Localisation;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class LocalisationServiceShould
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 21, 0, 0, 0, TimeSpan.Zero);

    private static LocalisationService Create(string language, string culture = "en-GB")
    {
        var catalogues = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en-GB"] = new Dictionary<string, string>
            {
                ["Common.Hello"] = "Hello",
                ["Common.OnlyDefault"] = "Default only",
                ["Common.Items"] = "{count} items synced on {date}",
                ["Common.Twice"] = "{name} and {name}"
            },
            ["it-IT"] = new Dictionary<string, string> { ["Common.Hello"] = "Ciao" }
        };

        return new LocalisationService(new CultureService(culture), catalogues, language);
    }

    [Fact]
    public void return_the_text_of_the_active_language() => Create("it-IT").Get("Common.Hello").ShouldBe("Ciao");

    [Fact]
    public void fall_back_to_the_default_language_for_a_missing_key() => Create("it-IT").Get("Common.OnlyDefault").ShouldBe("Default only");

    [Fact]
    public void return_the_marked_key_when_no_language_has_it() => Create("en-GB").Get("Common.Nope").ShouldBe("!Common.Nope!");

    [Fact]
    public void fill_template_parameters()
    {
        var text = Create("en-GB").Format("Common.Items", ("count", 1234), ("date", Moment));

        text.ShouldBe("1,234 items synced on 21/11/2026");
    }

    [Fact]
    public void format_parameters_for_the_culture_not_the_language()
    {
        var text = Create("en-GB", "en-US").Format("Common.Items", ("count", 5), ("date", Moment));

        text.ShouldBe("5 items synced on 11/21/2026");
    }

    [Fact]
    public void fill_a_repeated_parameter_everywhere_and_leave_unknown_ones()
    {
        var service = Create("en-GB");

        service.Format("Common.Twice", ("name", "A")).ShouldBe("A and A");
        service.Format("Common.Items", ("count", 1)).ShouldBe("1 items synced on {date}");
    }
}
