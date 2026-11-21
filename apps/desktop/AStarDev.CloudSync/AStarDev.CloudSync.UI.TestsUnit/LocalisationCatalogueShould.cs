using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class LocalisationCatalogueShould
{
    [Fact]
    public void merge_one_resource_per_feature()
    {
        var result = LocalisationCatalogue.Merge([new LocalisationResource("Common", """{"Common.A":"a"}"""), new LocalisationResource("Queue", """{"Queue.B":"b"}""")]);

        var ok = result.Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>();
        ok.Value["Common.A"].ShouldBe("a");
        ok.Value["Queue.B"].ShouldBe("b");
    }

    [Fact]
    public void reject_a_key_outside_its_feature()
    {
        var result = LocalisationCatalogue.Merge([new LocalisationResource("Queue", """{"Common.A":"a"}""")]);

        result.Value.ShouldBeOfType<Fail<LocalisationError>>().Error.Value.ShouldBeOfType<ForeignKey>();
    }

    [Fact]
    public void reject_a_duplicate_key()
    {
        var result = LocalisationCatalogue.Merge([new LocalisationResource("Common", """{"Common.A":"a"}"""), new LocalisationResource("Common", """{"Common.A":"b"}""")]);

        result.Value.ShouldBeOfType<Fail<LocalisationError>>().Error.Value.ShouldBeOfType<DuplicateKey>();
    }

    [Fact]
    public void reject_malformed_json()
    {
        var result = LocalisationCatalogue.Merge([new LocalisationResource("Common", "{not json")]);

        result.Value.ShouldBeOfType<Fail<LocalisationError>>().Error.Value.ShouldBeOfType<MalformedResource>();
    }

    [Fact]
    public void load_every_embedded_feature_file_for_the_default_language()
    {
        var result = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB");

        result.Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value.ShouldContainKey("Common.AppTitle");
    }

    [Fact]
    public void load_nothing_for_a_language_without_resources()
    {
        var result = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "xx-XX");

        result.Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value.ShouldBeEmpty();
    }
}
