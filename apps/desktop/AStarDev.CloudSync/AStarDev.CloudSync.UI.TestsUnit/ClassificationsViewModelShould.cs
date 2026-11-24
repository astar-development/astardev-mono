using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ClassificationsViewModelShould
{
    private static readonly Guid Colour = Guid.NewGuid();
    private static readonly Guid Place = Guid.NewGuid();
    private static readonly Guid Red = Guid.NewGuid();
    private static readonly Guid Paris = Guid.NewGuid();
    private static readonly Guid Blue = Guid.NewGuid();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ClassificationTermsView Vocabulary() => new(
        [new ClassificationCategoryView(Colour, "Colour"), new ClassificationCategoryView(Place, "Place")],
        [
            new ClassificationTermView(Red, "red", Colour, "Seed", false, 12),
            new ClassificationTermView(Paris, "paris", Place, "User", false, 3),
            new ClassificationTermView(Blue, "blue", Colour, "Seed", true, 0)
        ]);

    private static (Microsoft.Extensions.Hosting.IHost Host, ClassificationsViewModel Screen) Create(FakeIpcClient client)
    {
        var host = OperationScreensHost.Create(client);

        return (host, host.Screen<ClassificationsViewModel>());
    }

    private static FakeIpcClient Client() => new([], available: true) { ClassificationTermsReply = new Ok<ClassificationTermsView>(Vocabulary()) };

    [Fact]
    public void be_the_classifications_screen()
    {
        var (host, screen) = Create(Client());
        using var lifetime = host;

        screen.Id.ShouldBe(ScreenId.Classifications);
        screen.Title.ShouldBe("Classifications");
    }

    [Fact]
    public async Task show_the_terms_with_their_category_source_and_file_count()
    {
        var (host, screen) = Create(Client());
        using var lifetime = host;

        await screen.RefreshAsync(Token);

        screen.Terms.Select(term => term.Header).ShouldBe(["blue (ignored)", "paris", "red"]);
        screen.Terms.Single(term => term.Id == Red).Detail.ShouldBe("Colour  ·  Seed  ·  12 files");
        screen.IsEmpty.ShouldBeFalse();
        screen.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task order_by_a_column_and_reverse_it_when_chosen_again()
    {
        var (host, screen) = Create(Client());
        using var lifetime = host;
        await screen.RefreshAsync(Token);

        screen.SortByFilesCommand.Execute(null);
        var ascending = screen.Terms.Select(term => term.Id).ToList();
        screen.SortByFilesCommand.Execute(null);
        var descending = screen.Terms.Select(term => term.Id).ToList();
        screen.SortByCategoryCommand.Execute(null);

        ascending.ShouldBe([Blue, Paris, Red]);
        descending.ShouldBe([Red, Paris, Blue]);
        (screen.SortColumn, screen.SortDescending).ShouldBe((TermSortColumn.Category, false));
        screen.Terms.Select(term => term.Id).ShouldBe([Blue, Red, Paris]);
    }

    [Fact]
    public async Task filter_by_term_or_category_ignoring_case()
    {
        var (host, screen) = Create(Client());
        using var lifetime = host;
        await screen.RefreshAsync(Token);

        screen.Filter = " PLACE ";
        var byCategory = screen.Terms.Select(term => term.Id).ToList();
        screen.Filter = "RE";
        var byText = screen.Terms.Select(term => term.Id).ToList();
        screen.Filter = "nothing";

        byCategory.ShouldBe([Paris]);
        byText.ShouldBe([Red]);
        screen.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task load_the_files_of_the_selected_term_a_page_at_a_time()
    {
        var asked = new List<GetTermFiles>();
        var client = Client();
        client.TermFilesReply = query =>
        {
            asked.Add(query);

            return new Ok<TermFilesView>(new TermFilesView(3, [.. Enumerable.Range(query.Skip, Math.Min(2, 3 - query.Skip)).Select(index => new TermFileView(Guid.NewGuid(), $"Docs/{index}.jpg", "Ada"))]));
        };
        var (host, screen) = Create(client);
        using var lifetime = host;
        await screen.RefreshAsync(Token);

        screen.SelectedTerm = screen.Terms.Single(term => term.Id == Red);
        await screen.ReloadFilesCommand.ExecuteAsync(null);
        await screen.LoadFilesAsync(false, Token);

        asked.Select(query => (query.TermId, query.Skip)).ShouldContain((Red, 0));
        screen.Files.Select(file => file.Path).ShouldBe(["Docs/0.jpg", "Docs/1.jpg", "Docs/2.jpg"]);
        screen.FilesSummary.ShouldBe("Showing 3 of 3");
        screen.CanLoadMoreFiles.ShouldBeFalse();
    }

    [Fact]
    public async Task say_when_the_selected_term_has_no_files()
    {
        var (host, screen) = Create(Client());
        using var lifetime = host;
        await screen.RefreshAsync(Token);

        screen.SelectedTerm = screen.Terms.Single(term => term.Id == Blue);
        await screen.LoadFilesAsync(true, Token);

        screen.HasNoFiles.ShouldBeTrue();
        screen.Files.ShouldBeEmpty();
    }

    [Fact]
    public async Task keep_the_terms_and_say_why_when_the_daemon_is_unreachable()
    {
        var client = Client();
        var (host, screen) = Create(client);
        using var lifetime = host;
        await screen.RefreshAsync(Token);
        var unreachable = new FakeIpcClient([], available: false);
        using var other = OperationScreensHost.Create(unreachable);
        var failing = other.Screen<ClassificationsViewModel>();

        await failing.RefreshAsync(Token);

        screen.Terms.Count.ShouldBe(3);
        failing.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
        failing.Terms.ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var (host, screen) = Create(Client());
        using var lifetime = host;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => screen.RefreshAsync(cancelled.Token));
    }
}
