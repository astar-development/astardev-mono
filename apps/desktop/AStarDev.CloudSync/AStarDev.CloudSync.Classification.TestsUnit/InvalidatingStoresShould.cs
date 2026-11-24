using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class InvalidatingStoresShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task drop_the_cached_vocabulary_when_a_term_is_added()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        var category = new Category(new CategoryId(Guid.NewGuid()), "Colour");
        await host.Services.GetRequiredService<ICategoryStore>().AddAsync(category, Token);
        var provider = host.Services.GetRequiredService<IClassificationVocabularyProvider>();
        var before = (await provider.GetAsync(Token)).Value.ShouldBeOfType<Ok<ClassificationVocabulary>>().Value;

        await host.Services.GetRequiredService<ITermStore>().AddAsync(new Term(new TermId(Guid.NewGuid()), "red", category.Id, TermSource.User, TermStatus.Active), Token);

        var after = (await provider.GetAsync(Token)).Value.ShouldBeOfType<Ok<ClassificationVocabulary>>().Value;
        (before.Terms.Count, after.Terms.Count).ShouldBe((0, 1));
    }

    [Fact]
    public async Task drop_the_cached_vocabulary_when_a_category_is_renamed()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        var category = new Category(new CategoryId(Guid.NewGuid()), "Colour");
        var categories = host.Services.GetRequiredService<ICategoryStore>();
        await categories.AddAsync(category, Token);
        var provider = host.Services.GetRequiredService<IClassificationVocabularyProvider>();
        await provider.GetAsync(Token);

        await categories.RenameAsync(category.Id, "Colours", Token);

        (await provider.GetAsync(Token)).Value.ShouldBeOfType<Ok<ClassificationVocabulary>>().Value.CategoriesById[category.Id].Name.ShouldBe("Colours");
    }

    [Fact]
    public async Task keep_the_cached_vocabulary_when_a_write_fails()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        var provider = host.Services.GetRequiredService<IClassificationVocabularyProvider>();
        var before = provider.Generation;

        await host.Services.GetRequiredService<ITermStore>().RemoveAsync(new TermId(Guid.NewGuid()), Token);

        provider.Generation.ShouldBe(before);
    }

    [Fact]
    public async Task edit_a_term_through_the_editor_and_make_it_a_user_term_when_its_text_changes()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        var category = new Category(new CategoryId(Guid.NewGuid()), "Colour");
        await host.Services.GetRequiredService<ICategoryStore>().AddAsync(category, Token);
        var term = new Term(new TermId(Guid.NewGuid()), "red", category.Id, TermSource.Seed, TermStatus.Active);
        await host.Services.GetRequiredService<ITermStore>().AddAsync(term, Token);
        var editor = host.Services.GetRequiredService<IClassificationTermEditor>();

        var unchanged = await editor.UpdateAsync(term.Id, "RED", category.Id, true, Token);
        var renamed = await editor.UpdateAsync(term.Id, " Crimson ", category.Id, false, Token);

        unchanged.Value.ShouldBeOfType<Ok<Term>>().Value.ShouldBe(term with { Status = TermStatus.Ignored });
        renamed.Value.ShouldBeOfType<Ok<Term>>().Value.ShouldBe(term with { Text = "crimson", Source = TermSource.User });
    }

    [Fact]
    public async Task refuse_to_edit_with_blank_text_or_a_term_that_is_not_stored()
    {
        await using var host = await ClassificationTestHost.CreateAsync(Token);
        var editor = host.Services.GetRequiredService<IClassificationTermEditor>();
        var missing = new TermId(Guid.NewGuid());

        (await editor.UpdateAsync(missing, " ", default, false, Token)).Value.ShouldBeOfType<Fail<ClassificationError>>().Error.Value.ShouldBe(new InvalidTermText(" "));
        (await editor.UpdateAsync(missing, "red", default, false, Token)).Value.ShouldBeOfType<Fail<ClassificationError>>().Error.Value.ShouldBe(new TermNotFound(missing));
    }
}
