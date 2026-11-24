using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class EmbeddedClassificationSeedSourceShould
{
    private static readonly System.Reflection.Assembly Assembly = typeof(ClassificationServiceCollectionExtensions).Assembly;

    [Fact]
    public async Task load_the_shipped_seed()
    {
        var result = await new EmbeddedClassificationSeedSource(Assembly, "AStarDev.CloudSync.Classification.Seed.").LoadAsync(TestContext.Current.CancellationToken);

        var seed = result.Value.ShouldBeOfType<Ok<ClassificationSeed>>().Value;
        seed.Version.ShouldNotBeNullOrWhiteSpace();
        seed.Categories.ShouldNotBeEmpty();
        seed.Terms.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task name_only_defined_categories_in_every_shipped_term()
    {
        var result = await new EmbeddedClassificationSeedSource(Assembly, "AStarDev.CloudSync.Classification.Seed.").LoadAsync(TestContext.Current.CancellationToken);

        var seed = result.Value.ShouldBeOfType<Ok<ClassificationSeed>>().Value;
        seed.Terms.Select(term => term.CategoryName).Distinct(StringComparer.OrdinalIgnoreCase).Except(seed.Categories, StringComparer.OrdinalIgnoreCase).ShouldBeEmpty();
    }

    [Fact]
    public async Task report_an_invalid_seed_when_no_file_matches_the_prefix()
    {
        var result = await new EmbeddedClassificationSeedSource(Assembly, "No.Such.Prefix.").LoadAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<InvalidSeed>>();
    }
}
