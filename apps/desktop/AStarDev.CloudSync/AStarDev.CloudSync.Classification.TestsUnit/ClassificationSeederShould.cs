using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class ClassificationSeederShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task apply_the_shipped_seed_once_per_version()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        var seeder = host.Services.GetRequiredService<ClassificationSeeder>();

        var first = await seeder.SeedAsync(Token);
        var second = await seeder.SeedAsync(Token);

        first.Value.ShouldBeOfType<SeedApplied>();
        second.Value.ShouldBeOfType<SeedAlreadyCurrent>();
    }

    [Fact]
    public async Task store_the_categories_and_terms_of_the_seed()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token, services => services.AddSingleton<IClassificationSeedSource>(new FixedSeedSource(new Ok<ClassificationSeed>(FixedSeedSource.Seed("1", new SeedTerm(" Red ", "Colour"), new SeedTerm("red", "colour"), new SeedTerm("   ", "Colour"))))));

        await host.Services.GetRequiredService<ClassificationSeeder>().SeedAsync(Token);

        var stored = (await host.Services.GetRequiredService<ITermStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Term>>>().Value;
        stored.Select(term => (term.Text, term.Source, term.Status)).ShouldBe([("red", TermSource.Seed, TermStatus.Active)]);
        (await host.Services.GetRequiredService<ICategoryStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Category>>>().Value.Select(category => category.Name).ShouldBe(["Colour", "Place"]);
    }

    [Fact]
    public async Task keep_a_term_the_user_ignored_when_a_new_seed_version_arrives()
    {
        var source = new SwitchableSeedSource(FixedSeedSource.Seed("1", new SeedTerm("red", "Colour")));
        await using var host = await ClassificationTestHost.CreateAsync(Token, services => services.AddSingleton<IClassificationSeedSource>(source));
        var seeder = host.Services.GetRequiredService<ClassificationSeeder>();
        var terms = host.Services.GetRequiredService<ITermStore>();
        await seeder.SeedAsync(Token);
        var red = (await terms.ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Term>>>().Value.Single();
        await terms.UpdateAsync(red with { Status = TermStatus.Ignored }, Token);
        source.Seed = FixedSeedSource.Seed("2", new SeedTerm("red", "Colour"), new SeedTerm("blue", "Colour"));

        var outcome = await seeder.SeedAsync(Token);

        outcome.Value.ShouldBeOfType<SeedApplied>();
        (await terms.ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Term>>>().Value.Select(term => (term.Text, term.Status)).ShouldBe([("blue", TermStatus.Active), ("red", TermStatus.Ignored)]);
    }

    [Fact]
    public async Task fail_without_recording_the_version_when_a_term_names_an_unknown_category()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token, services => services.AddSingleton<IClassificationSeedSource>(new FixedSeedSource(new Ok<ClassificationSeed>(FixedSeedSource.Seed("1", new SeedTerm("red", "Nope"))))));

        var outcome = await host.Services.GetRequiredService<ClassificationSeeder>().SeedAsync(Token);

        outcome.Value.ShouldBeOfType<SeedFailed>();
        (await host.Services.GetRequiredService<IClassificationStateStore>().GetSeedVersionAsync(Token)).Value.ShouldBeOfType<Ok<Option<string>>>().Value.Value.ShouldBeOfType<None<string>>();
    }

    [Fact]
    public async Task fail_when_the_seed_cannot_be_loaded()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token, services => services.AddSingleton<IClassificationSeedSource>(new FixedSeedSource(new Fail<InvalidSeed>(new InvalidSeed("broken")))));

        var outcome = await host.Services.GetRequiredService<ClassificationSeeder>().SeedAsync(Token);

        outcome.Value.ShouldBeOfType<SeedFailed>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ClassificationSeeder>().SeedAsync(cancelled.Token));
    }

    private sealed class SwitchableSeedSource(ClassificationSeed seed) : IClassificationSeedSource
    {
        public ClassificationSeed Seed { get; set; } = seed;

        public Task<Result<ClassificationSeed, InvalidSeed>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<Result<ClassificationSeed, InvalidSeed>>(new Ok<ClassificationSeed>(Seed));
    }
}
