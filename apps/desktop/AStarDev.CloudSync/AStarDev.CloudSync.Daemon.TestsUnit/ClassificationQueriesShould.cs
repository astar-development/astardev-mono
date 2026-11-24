using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using static AStarDev.CloudSync.Daemon.TestsUnit.ClassificationDaemonTestData;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ClassificationQueriesShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task return_terms_with_file_counts()
    {
        await using var host = await SeededHostAsync(Token);
        await SaveFilesAsync(host, Token, "Photos/a.jpg", "Photos/b.jpg");

        var view = (await host.Services.GetRequiredService<TermsQuery>().HandleAsync(new GetClassificationTerms(), Token)).Value.ShouldBeOfType<Ok<ClassificationTermsView>>().Value;

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

        var view = (await host.Services.GetRequiredService<TermsQuery>().HandleAsync(new GetClassificationTerms(), Token)).Value.ShouldBeOfType<Ok<ClassificationTermsView>>().Value;

        view.Terms.Single(term => term.Text == "jpg").Ignored.ShouldBeTrue();
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
        view.Suggestions.Single(suggestion => suggestion.Word == "jennifer").IsFirstName.ShouldBeTrue();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await SeededHostAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<TermsQuery>().HandleAsync(new GetClassificationTerms(), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<TermFilesQuery>().HandleAsync(new GetTermFiles(Guid.NewGuid(), 0, 10), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<SuggestionsQuery>().HandleAsync(new GetClassificationSuggestions(), cancelled.Token));
    }
}
