using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using static AStarDev.CloudSync.Classification.TestsUnit.SyncTestData;

namespace AStarDev.CloudSync.Classification.TestsUnit;

public sealed class ClassificationSuggestionsShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task rank_unreviewed_words_by_file_count_then_alphabetically()
    {
        await using var host = await SeededHostAsync();
        await SaveFilesAsync(host, Token, "Zorblax/plimbus.jpg", "Zorblax/Quillon.jpg", "Quillon/other.txt");

        var suggestions = await SuggestionsAsync(host);

        suggestions.Select(suggestion => (suggestion.Word, suggestion.FileCount)).Take(3).ShouldBe([("quillon", 2), ("zorblax", 2), ("plimbus", 1)]);
    }

    [Fact]
    public async Task leave_out_words_that_are_already_terms_in_any_status()
    {
        await using var host = await SeededHostAsync();
        await SaveFilesAsync(host, Token, "Holiday/Zorblax.jpg");

        (await SuggestionsAsync(host)).Select(suggestion => suggestion.Word).ShouldBe(["zorblax"]);
    }

    [Fact]
    public async Task leave_out_common_english_words()
    {
        await using var host = await SeededHostAsync();
        await SaveFilesAsync(host, Token, "house/Zorblax.jpg");

        (await SuggestionsAsync(host)).Select(suggestion => suggestion.Word).ShouldBe(["zorblax"]);
    }

    [Fact]
    public async Task hint_that_a_word_is_a_first_name()
    {
        await using var host = await SeededHostAsync();
        await SaveFilesAsync(host, Token, "Zorblax/Jennifer.jpg");

        (await SuggestionsAsync(host)).Single(suggestion => suggestion.Word == "jennifer").Hint.ShouldBe(SuggestionHint.FirstName);
    }

    [Fact]
    public async Task promote_a_word_to_an_active_user_term_that_stops_being_suggested()
    {
        await using var host = await SeededHostAsync();
        await SaveFilesAsync(host, Token, "Zorblax/plimbus.jpg");
        var category = await FirstCategoryAsync(host);

        var promoted = await host.Services.GetRequiredService<IClassificationSuggestions>().PromoteAsync(" Zorblax ", category.Id, Token);

        var term = promoted.Value.ShouldBeOfType<Ok<Term>>().Value;
        (term.Text, term.Source, term.Status).ShouldBe(("zorblax", TermSource.User, TermStatus.Active));
        (await SuggestionsAsync(host)).Select(suggestion => suggestion.Word).ShouldNotContain("zorblax");
    }

    [Fact]
    public async Task give_one_term_for_parallel_promotes()
    {
        await using var host = await SeededHostAsync();
        var category = await FirstCategoryAsync(host);
        var suggestions = host.Services.GetRequiredService<IClassificationSuggestions>();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => suggestions.PromoteAsync("zorblax", category.Id, Token)));

        (await host.Services.GetRequiredService<ITermStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Term>>>().Value.Count(term => term.Text == "zorblax").ShouldBe(1);
    }

    [Fact]
    public async Task not_suggest_an_ignored_word_again()
    {
        await using var host = await SeededHostAsync();
        await SaveFilesAsync(host, Token, "Zorblax/plimbus.jpg");
        var category = await FirstCategoryAsync(host);

        await host.Services.GetRequiredService<IClassificationSuggestions>().IgnoreAsync("zorblax", category.Id, Token);

        (await SuggestionsAsync(host)).Select(suggestion => suggestion.Word).ShouldNotContain("zorblax");
    }

    [Fact]
    public async Task promote_a_previously_ignored_word()
    {
        await using var host = await SeededHostAsync();
        var category = await FirstCategoryAsync(host);
        var suggestions = host.Services.GetRequiredService<IClassificationSuggestions>();
        await suggestions.IgnoreAsync("zorblax", category.Id, Token);

        var promoted = await suggestions.PromoteAsync("zorblax", category.Id, Token);

        promoted.Value.ShouldBeOfType<Ok<Term>>().Value.Status.ShouldBe(TermStatus.Active);
    }

    [Fact]
    public async Task refuse_a_blank_word()
    {
        await using var host = await SeededHostAsync();
        var category = await FirstCategoryAsync(host);

        var result = await host.Services.GetRequiredService<IClassificationSuggestions>().PromoteAsync("  ", category.Id, Token);

        result.Value.ShouldBeOfType<Fail<ClassificationError>>().Error.Value.ShouldBe(new InvalidTermText("  "));
    }

    [Fact]
    public async Task refuse_a_category_that_is_not_stored()
    {
        await using var host = await SeededHostAsync();
        var missing = new CategoryId(Guid.NewGuid());

        var result = await host.Services.GetRequiredService<IClassificationSuggestions>().PromoteAsync("zorblax", missing, Token);

        result.Value.ShouldBeOfType<Fail<ClassificationError>>().Error.Value.ShouldBe(new CategoryNotFound(missing));
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await SeededHostAsync();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IClassificationSuggestions>().GetSuggestionsAsync(cancelled.Token));
    }

    private static async Task<ClassificationTestHost> SeededHostAsync()
    {
        var seed = FixedSeedSource.Seed("1", new SeedTerm("jpg", "Colour"), new SeedTerm("txt", "Colour"), new SeedTerm("holiday", "Place"));
        var host = await ClassificationTestHost.CreateAsync(Token, services => services.AddSingleton<IClassificationSeedSource>(new FixedSeedSource(new Ok<ClassificationSeed>(seed))));
        await host.Services.GetRequiredService<ClassificationSeeder>().SeedAsync(Token);

        return host;
    }

    private static async Task<Category> FirstCategoryAsync(ClassificationTestHost host) =>
        (await host.Services.GetRequiredService<ICategoryStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Category>>>().Value.First();

    private static async Task<IReadOnlyList<ClassificationSuggestion>> SuggestionsAsync(ClassificationTestHost host) =>
        (await host.Services.GetRequiredService<IClassificationSuggestions>().GetSuggestionsAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<ClassificationSuggestion>>>().Value;
}
