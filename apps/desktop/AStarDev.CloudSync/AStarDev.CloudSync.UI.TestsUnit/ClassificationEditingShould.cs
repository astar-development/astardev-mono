using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ClassificationEditingShould
{
    private static readonly Guid Colour = Guid.NewGuid();
    private static readonly Guid Red = Guid.NewGuid();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FakeIpcClient Client() => new([], available: true)
    {
        ClassificationTermsReply = new Ok<ClassificationTermsView>(new ClassificationTermsView([new ClassificationCategoryView(Colour, "Colour")], [new ClassificationTermView(Red, "red", Colour, "Seed", false, 2)]))
    };

    private static (Microsoft.Extensions.Hosting.IHost Host, ClassificationsViewModel Screen) Create(FakeIpcClient client)
    {
        var host = OperationScreensHost.Create(client);

        return (host, host.Screen<ClassificationsViewModel>());
    }

    private static LocalisationService RealLocalisation(CultureService culture)
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(culture, new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10, Token);

        condition().ShouldBeTrue();
    }

    [Fact]
    public async Task send_a_new_category_and_read_the_terms_again()
    {
        var client = Client();
        var (host, screen) = Create(client);
        using var lifetime = host;
        await screen.RefreshAsync(Token);
        screen.Editor.NewCategoryName = "Weather";

        await screen.Editor.AddCategoryCommand.ExecuteAsync(null);

        client.Sent.ShouldContain(new CreateCategory("Weather"));
        screen.Editor.NewCategoryName.ShouldBeEmpty();
        screen.Editor.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task show_the_daemons_reason_and_keep_the_text_when_a_change_is_refused()
    {
        var client = Client();
        client.CommandReply = new Ok<CommandResult>(new CommandResult(CommandOutcome.Rejected, "A category with that name already exists."));
        var (host, screen) = Create(client);
        using var lifetime = host;
        await screen.RefreshAsync(Token);
        screen.Editor.NewCategoryName = "Colour";

        await screen.Editor.AddCategoryCommand.ExecuteAsync(null);

        screen.Editor.ErrorMessage.ShouldBe("A category with that name already exists.");
        screen.Editor.NewCategoryName.ShouldBe("Colour");
    }

    [Fact]
    public async Task rename_and_remove_the_chosen_category()
    {
        var client = Client();
        var (host, screen) = Create(client);
        using var lifetime = host;
        await screen.RefreshAsync(Token);

        screen.Editor.SelectedCategory = screen.Editor.Categories.Single();
        screen.Editor.RenameText = "Colours";
        await screen.Editor.RenameCategoryCommand.ExecuteAsync(null);
        await screen.Editor.DeleteCategoryCommand.ExecuteAsync(null);

        client.Sent.ShouldBe([new RenameCategory(Colour, "Colours"), new DeleteCategory(Colour)]);
    }

    [Fact]
    public async Task add_a_term_to_the_chosen_category()
    {
        var client = Client();
        var (host, screen) = Create(client);
        using var lifetime = host;
        await screen.RefreshAsync(Token);
        screen.Editor.NewTermText = "Crimson";

        await screen.Editor.AddTermCommand.ExecuteAsync(null);

        client.Sent.ShouldBe([new CreateTerm("Crimson", Colour)]);
        screen.Editor.NewTermText.ShouldBeEmpty();
    }

    [Fact]
    public async Task start_the_edit_form_from_the_selected_term_and_save_it()
    {
        var client = Client();
        var (host, screen) = Create(client);
        using var lifetime = host;
        await screen.RefreshAsync(Token);

        screen.SelectedTerm = screen.Terms.Single();
        screen.Editor.EditText = "scarlet";
        screen.Editor.EditIgnored = true;
        await screen.Editor.SaveTermCommand.ExecuteAsync(null);
        await screen.Editor.DeleteTermCommand.ExecuteAsync(null);

        screen.Editor.HasSelectedTerm.ShouldBeFalse();
        client.Sent.ShouldBe([new UpdateTerm(Red, "scarlet", Colour, true), new DeleteTerm(Red)]);
    }

    [Fact]
    public async Task scan_for_suggestions_the_first_time_the_tab_is_shown_and_only_then()
    {
        var client = Client();
        client.SuggestionsReply = new Ok<ClassificationSuggestionsView>(new ClassificationSuggestionsView([new ClassificationSuggestionView("zorblax", 4, false), new ClassificationSuggestionView("jennifer", 2, true)]));
        var (host, screen) = Create(client);
        using var lifetime = host;

        await screen.Suggestions.EnsureLoadedAsync(Token);
        client.SuggestionsReply = new Ok<ClassificationSuggestionsView>(new ClassificationSuggestionsView([]));
        await screen.Suggestions.EnsureLoadedAsync(Token);

        screen.Suggestions.Rows.Select(row => (row.Word, row.Detail)).ShouldBe([("zorblax", "in 4 files"), ("jennifer", "in 2 files  ·  looks like a first name")]);
        screen.Suggestions.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task promote_and_ignore_a_word_into_the_chosen_category_and_drop_it_from_the_list()
    {
        var client = Client();
        client.SuggestionsReply = new Ok<ClassificationSuggestionsView>(new ClassificationSuggestionsView([new ClassificationSuggestionView("zorblax", 4, false), new ClassificationSuggestionView("plimbus", 2, false)]));
        var (host, screen) = Create(client);
        using var lifetime = host;
        await screen.RefreshAsync(Token);
        await screen.Suggestions.LoadAsync(Token);
        screen.Suggestions.Category = screen.Editor.Categories.Single();

        await screen.Suggestions.PromoteCommand.ExecuteAsync(screen.Suggestions.Rows[0]);
        await screen.Suggestions.IgnoreCommand.ExecuteAsync(screen.Suggestions.Rows[0]);

        client.Sent.ShouldBe([new PromoteSuggestion("zorblax", Colour), new IgnoreSuggestion("plimbus", Colour)]);
        screen.Suggestions.Rows.ShouldBeEmpty();
        screen.Suggestions.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task ask_for_a_category_before_promoting()
    {
        var client = Client();
        client.SuggestionsReply = new Ok<ClassificationSuggestionsView>(new ClassificationSuggestionsView([new ClassificationSuggestionView("zorblax", 4, false)]));
        var (host, screen) = Create(client);
        using var lifetime = host;
        await screen.Suggestions.LoadAsync(Token);

        await screen.Suggestions.PromoteCommand.ExecuteAsync(screen.Suggestions.Rows[0]);

        screen.Suggestions.ErrorMessage.ShouldBe("Choose a category first.");
        client.Sent.ShouldBeEmpty();
        screen.Suggestions.Rows.Count.ShouldBe(1);
    }

    [Fact]
    public async Task start_a_reclassify_follow_its_progress_and_read_the_terms_again_when_it_ends()
    {
        var client = Client();
        var connection = new ConnectionViewModel(() => client, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        var culture = new CultureService("en-GB");
        var reclassify = new ClassificationReclassifyViewModel(RealLocalisation(culture), culture, () => client, connection);
        var finished = 0;
        reclassify.Finished += () => finished++;
        using var source = new CancellationTokenSource();
        _ = connection.RunAsync(source.Token);
        await WaitForAsync(() => connection.State == ConnectionState.Connected);

        await reclassify.StartCommand.ExecuteAsync(null);
        client.Raise(new ReclassifyProgressChanged(true, 1200));
        var duringRun = (reclassify.IsRunning, reclassify.StatusText, reclassify.StartCommand.CanExecute(null));
        client.Raise(new ReclassifyProgressChanged(false, 3400));

        client.Sent.ShouldBe([new StartReclassify()]);
        duringRun.ShouldBe((true, "Classifying… 1,200 files so far", false));
        reclassify.IsRunning.ShouldBeFalse();
        reclassify.StatusText.ShouldStartWith("Last run went through 3,400 files");
        finished.ShouldBe(1);
        await source.CancelAsync();
    }

    [Fact]
    public async Task show_the_status_of_a_run_that_started_elsewhere()
    {
        var client = Client();
        client.ReclassifyStatusReply = new Ok<ReclassifyStatusView>(new ReclassifyStatusView(true, 500, null));
        var (host, screen) = Create(client);
        using var lifetime = host;

        await screen.Reclassify.LoadStatusAsync(Token);

        screen.Reclassify.IsRunning.ShouldBeTrue();
        screen.Reclassify.StatusText.ShouldContain("500");
    }

    [Fact]
    public async Task say_when_no_run_has_finished()
    {
        var (host, screen) = Create(Client());
        using var lifetime = host;

        await screen.Reclassify.LoadStatusAsync(Token);

        screen.Reclassify.StatusText.ShouldBe("No run has finished yet");
    }
}
