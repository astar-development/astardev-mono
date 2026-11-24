using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class ClassificationVocabularyProviderShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task return_the_stored_terms_and_categories()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        await host.Services.GetRequiredService<ClassificationSeeder>().SeedAsync(Token);

        var result = await host.Services.GetRequiredService<IClassificationVocabularyProvider>().GetAsync(Token);

        var vocabulary = result.Value.ShouldBeOfType<Ok<ClassificationVocabulary>>().Value;
        vocabulary.Terms.ShouldNotBeEmpty();
        vocabulary.CategoriesById.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task keep_the_vocabulary_until_it_is_invalidated()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        var provider = host.Services.GetRequiredService<IClassificationVocabularyProvider>();
        var first = (await provider.GetAsync(Token)).Value.ShouldBeOfType<Ok<ClassificationVocabulary>>().Value;

        var second = (await provider.GetAsync(Token)).Value.ShouldBeOfType<Ok<ClassificationVocabulary>>().Value;
        provider.Invalidate();
        var third = (await provider.GetAsync(Token)).Value.ShouldBeOfType<Ok<ClassificationVocabulary>>().Value;

        second.ShouldBeSameAs(first);
        third.ShouldNotBeSameAs(first);
    }

    [Fact]
    public async Task count_each_invalidation()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        var provider = host.Services.GetRequiredService<IClassificationVocabularyProvider>();
        var before = provider.Generation;

        provider.Invalidate();

        provider.Generation.ShouldBe(before + 1);
    }

    [Fact]
    public async Task drop_ignored_terms_from_the_vocabulary()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        var category = new Category(new CategoryId(Guid.NewGuid()), "Colour");
        await host.Services.GetRequiredService<ICategoryStore>().AddAsync(category, Token);
        await host.Services.GetRequiredService<ITermStore>().AddAsync(new Term(new TermId(Guid.NewGuid()), "red", category.Id, TermSource.User, TermStatus.Ignored), Token);

        var result = await host.Services.GetRequiredService<IClassificationVocabularyProvider>().GetAsync(Token);

        result.Value.ShouldBeOfType<Ok<ClassificationVocabulary>>().Value.Terms.ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IClassificationVocabularyProvider>().GetAsync(cancelled.Token));
    }
}
