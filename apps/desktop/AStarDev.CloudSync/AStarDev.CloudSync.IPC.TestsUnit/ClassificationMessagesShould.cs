namespace AStarDev.CloudSync.IPC.TestsUnit;

public sealed class ClassificationMessagesShould
{
    [Fact]
    public void round_trip_the_queries_and_the_command()
    {
        var termId = Guid.NewGuid();

        IpcJson.FromElement<GetClassificationTerms>(IpcJson.ToElement(new GetClassificationTerms()).ShouldSucceed()).ShouldSucceed().ShouldBe(new GetClassificationTerms());
        IpcJson.FromElement<GetTermFiles>(IpcJson.ToElement(new GetTermFiles(termId, 5, 100)).ShouldSucceed()).ShouldSucceed().ShouldBe(new GetTermFiles(termId, 5, 100));
        IpcJson.FromElement<GetClassificationSuggestions>(IpcJson.ToElement(new GetClassificationSuggestions()).ShouldSucceed()).ShouldSucceed().ShouldBe(new GetClassificationSuggestions());
        IpcJson.FromElement<StartReclassify>(IpcJson.ToElement(new StartReclassify()).ShouldSucceed()).ShouldSucceed().ShouldBe(new StartReclassify());
        IpcJson.FromElement<GetReclassifyStatus>(IpcJson.ToElement(new GetReclassifyStatus()).ShouldSucceed()).ShouldSucceed().ShouldBe(new GetReclassifyStatus());
    }

    [Fact]
    public void round_trip_a_terms_view()
    {
        var category = new ClassificationCategoryView(Guid.NewGuid(), "Colour");
        var view = new ClassificationTermsView([category], [new ClassificationTermView(Guid.NewGuid(), "red", category.Id, "Seed", false, 3)]);

        var back = IpcJson.FromElement<ClassificationTermsView>(IpcJson.ToElement(view).ShouldSucceed()).ShouldSucceed();

        back.Categories.ShouldBe(view.Categories);
        back.Terms.ShouldBe(view.Terms);
    }

    [Fact]
    public void round_trip_a_term_files_view()
    {
        var view = new TermFilesView(7, [new TermFileView(Guid.NewGuid(), "Docs/a.txt", "Ada")]);

        var back = IpcJson.FromElement<TermFilesView>(IpcJson.ToElement(view).ShouldSucceed()).ShouldSucceed();

        back.Total.ShouldBe(7);
        back.Files.ShouldBe(view.Files);
    }

    [Fact]
    public void round_trip_a_suggestions_view()
    {
        var view = new ClassificationSuggestionsView([new ClassificationSuggestionView("zorblax", 4, true)]);

        IpcJson.FromElement<ClassificationSuggestionsView>(IpcJson.ToElement(view).ShouldSucceed()).ShouldSucceed().Suggestions.ShouldBe(view.Suggestions);
    }

    [Fact]
    public void round_trip_the_status_and_the_progress_event()
    {
        var completed = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

        IpcJson.FromElement<ReclassifyStatusView>(IpcJson.ToElement(new ReclassifyStatusView(true, 10, completed)).ShouldSucceed()).ShouldSucceed().ShouldBe(new ReclassifyStatusView(true, 10, completed));
        IpcJson.FromElement<ReclassifyProgressChanged>(IpcJson.ToElement(new ReclassifyProgressChanged(false, 99)).ShouldSucceed()).ShouldSucceed().ShouldBe(new ReclassifyProgressChanged(false, 99));
    }

    [Fact]
    public void redact_the_text_of_a_term_a_file_and_a_suggestion()
    {
        new ClassificationTermView(Guid.NewGuid(), "secretword", Guid.NewGuid(), "Seed", false, 1).ToString().ShouldNotContain("secretword");
        new TermFileView(Guid.NewGuid(), "secret/path.txt", "Ada").ToString().ShouldNotContain("secret");
        new ClassificationSuggestionView("secretword", 1, false).ToString().ShouldNotContain("secretword");
    }
}
