using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using static AStarDev.CloudSync.Daemon.TestsIntegration.ClassificationDaemonTestData;

namespace AStarDev.CloudSync.Daemon.TestsIntegration;

public sealed class ClassificationQueriesShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task return_terms_with_file_counts()
    {
        await using var host = await SeededHostAsync(Token);
        await SaveFilesAsync(host, Token, "Photos/a.jpg", "Photos/b.jpg");

        var view = (await host.Services.GetRequiredService<TermsQuery>().HandleAsync(new GetClassificationTerms("jpg"), Token)).Value.ShouldBeOfType<Ok<ClassificationTermsView>>().Value;

        view.Categories.ShouldNotBeEmpty();
        view.Terms.Single(term => term.Text == "jpg").FileCount.ShouldBe(2);
    }

    [Fact]
    public async Task mark_an_ignored_term()
    {
        await using var host = await SeededHostAsync(Token);
        var terms = host.Services.GetRequiredService<ITermStore>();
        var jpg = (await terms.ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Term>>>().Value.Single(term => term.Text == "jpg");
        await terms.UpdateAsync(jpg with { Status = TermStatus.Ignored }, Token);

        var view = (await host.Services.GetRequiredService<TermsQuery>().HandleAsync(new GetClassificationTerms("jpg"), Token)).Value.ShouldBeOfType<Ok<ClassificationTermsView>>().Value;

        view.Terms.Single(term => term.Text == "jpg").Ignored.ShouldBeTrue();
    }

    [Fact]
    public async Task answer_the_seeded_vocabulary_in_pages_that_fit_one_ipc_message()
    {
        await using var host = await SeededHostAsync(Token);
        var query = host.Services.GetRequiredService<TermsQuery>();

        var first = (await query.HandleAsync(new GetClassificationTerms("", ClassificationTermSort.Text, false, 0, 100000), Token)).Value.ShouldBeOfType<Ok<ClassificationTermsView>>().Value;
        var second = (await query.HandleAsync(new GetClassificationTerms("", ClassificationTermSort.Text, false, 500, 500), Token)).Value.ShouldBeOfType<Ok<ClassificationTermsView>>().Value;

        first.Total.ShouldBeGreaterThan(30000);
        first.Terms.Count.ShouldBe(500);
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(first).Length.ShouldBeLessThan(1024 * 1024);
        second.Terms.Select(term => term.Id).Intersect(first.Terms.Select(term => term.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task filter_by_term_or_category_and_order_by_file_count()
    {
        await using var host = await SeededHostAsync(Token);
        await SaveFilesAsync(host, Token, "Photos/a.jpg", "Photos/b.jpg", "Docs/c.txt");
        var query = host.Services.GetRequiredService<TermsQuery>();

        var byText = (await query.HandleAsync(new GetClassificationTerms("JPG"), Token)).Value.ShouldBeOfType<Ok<ClassificationTermsView>>().Value;
        var busiest = (await query.HandleAsync(new GetClassificationTerms("", ClassificationTermSort.Files, true, 0, 2), Token)).Value.ShouldBeOfType<Ok<ClassificationTermsView>>().Value;

        byText.Terms.Select(term => term.Text).ShouldContain("jpg");
        byText.Total.ShouldBe(byText.Terms.Count);
        busiest.Terms.Select(term => term.FileCount).ShouldBe([.. busiest.Terms.Select(term => term.FileCount).OrderDescending()]);
        busiest.Terms[0].FileCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task name_the_account_for_each_file_of_a_term()
    {
        await using var host = await SeededHostAsync(Token);
        await SaveFilesAsync(host, Token, "Photos/a.jpg");
        var jpg = (await host.Services.GetRequiredService<ITermStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Term>>>().Value.Single(term => term.Text == "jpg");

        var view = (await host.Services.GetRequiredService<TermFilesQuery>().HandleAsync(new GetTermFiles(jpg.Id.Value, 0, 10), Token)).Value.ShouldBeOfType<Ok<TermFilesView>>().Value;

        view.Total.ShouldBe(1);
        view.Files.Select(file => (file.Path, file.AccountName)).ShouldBe([("Photos/a.jpg", "id@example.com")]);
    }

    [Fact]
    public async Task bring_a_page_request_into_range()
    {
        await using var host = await SeededHostAsync(Token);

        var view = (await host.Services.GetRequiredService<TermFilesQuery>().HandleAsync(new GetTermFiles(Guid.NewGuid(), -5, 100000), Token)).Value.ShouldBeOfType<Ok<TermFilesView>>().Value;

        (view.Total, view.Files.Count).ShouldBe((0, 0));
    }

    [Fact]
    public async Task return_suggestions_ranked_with_a_first_name_hint()
    {
        await using var host = await SeededHostAsync(Token);
        await SaveFilesAsync(host, Token, "Zorblax/Jennifer.jpg", "Zorblax/b.jpg");

        var view = (await host.Services.GetRequiredService<SuggestionsQuery>().HandleAsync(new GetClassificationSuggestions(), Token)).Value.ShouldBeOfType<Ok<ClassificationSuggestionsView>>().Value;

        view.Suggestions.First().ShouldBe(new ClassificationSuggestionView("zorblax", 2, false));
        view.Total.ShouldBeGreaterThanOrEqualTo(view.Suggestions.Count);
        view.Suggestions.Single(suggestion => suggestion.Word == "jennifer").IsFirstName.ShouldBeTrue();
    }

    [Fact]
    public async Task return_the_requested_page_of_suggestions_in_the_same_order_as_the_whole_list()
    {
        await using var host = await SeededHostAsync(Token);
        await SaveFilesAsync(host, Token, "Zorblax/Plimbus/Quexo.jpg", "Zorblax/Plimbus.jpg", "Zorblax.jpg");
        var query = host.Services.GetRequiredService<SuggestionsQuery>();
        var whole = (await query.HandleAsync(new GetClassificationSuggestions(), Token)).Value.ShouldBeOfType<Ok<ClassificationSuggestionsView>>().Value;

        var page = (await query.HandleAsync(new GetClassificationSuggestions(1, 1), Token)).Value.ShouldBeOfType<Ok<ClassificationSuggestionsView>>().Value;

        whole.Suggestions.Take(3).Select(suggestion => suggestion.Word).ShouldBe(["zorblax", "plimbus", "quexo"]);
        page.Suggestions.ShouldBe([whole.Suggestions[1]]);
        page.Total.ShouldBe(whole.Total);
    }

    [Fact]
    public async Task bring_a_suggestions_page_outside_the_limits_into_range()
    {
        await using var host = await SeededHostAsync(Token);
        await SaveFilesAsync(host, Token, "Zorblax/Plimbus/Quexo.jpg");
        var query = host.Services.GetRequiredService<SuggestionsQuery>();

        var wide = (await query.HandleAsync(new GetClassificationSuggestions(-5, 100000), Token)).Value.ShouldBeOfType<Ok<ClassificationSuggestionsView>>().Value;
        var empty = (await query.HandleAsync(new GetClassificationSuggestions(0, 0), Token)).Value.ShouldBeOfType<Ok<ClassificationSuggestionsView>>().Value;

        wide.Suggestions.Count.ShouldBe(wide.Total);
        empty.Suggestions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await SeededHostAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<TermsQuery>().HandleAsync(new GetClassificationTerms(), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<TermFilesQuery>().HandleAsync(new GetTermFiles(Guid.NewGuid(), 0, 10), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SuggestionsQuery>().HandleAsync(new GetClassificationSuggestions(0, 10), cancelled.Token));
    }
}
