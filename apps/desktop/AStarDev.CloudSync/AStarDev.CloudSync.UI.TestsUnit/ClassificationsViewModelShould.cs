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
        ], 3);

    private static async Task Ask(List<GetClassificationTerms> asked, Action change)
    {
        var before = asked.Count;
        change();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (asked.Count == before && DateTime.UtcNow < deadline) await Task.Delay(5, Token);

        await Task.Delay(50, Token);
    }

    private static (Microsoft.Extensions.Hosting.IHost Host, ClassificationsViewModel Screen) Create(FakeIpcClient client)
    {
        var host = OperationScreensHost.Create(client);

        return (host, host.Screen<ClassificationsViewModel>());
    }

    private static FakeIpcClient Client(List<GetClassificationTerms>? asked = null) => new([], available: true)
    {
        ClassificationTermsReply = query =>
        {
            asked?.Add(query);

            return new Ok<ClassificationTermsView>(Vocabulary());
        }
    };

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

        screen.Terms.Select(term => term.Header).ShouldBe(["red", "paris", "blue (ignored)"]);
        screen.Terms.Single(term => term.Id == Red).Detail.ShouldBe("Colour  ·  Seed  ·  12 files");
        screen.IsEmpty.ShouldBeFalse();
        screen.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task ask_the_daemon_for_the_order_and_reverse_it_when_the_column_is_chosen_again()
    {
        var asked = new List<GetClassificationTerms>();
        var (host, screen) = Create(Client(asked));
        using var lifetime = host;
        await screen.RefreshAsync(Token);

        await Ask(asked, () => screen.SortByFilesCommand.Execute(null));
        await Ask(asked, () => screen.SortByFilesCommand.Execute(null));
        await Ask(asked, () => screen.SortByCategoryCommand.Execute(null));

        asked.Select(query => (query.Sort, query.Descending, query.Skip)).ShouldBe([(ClassificationTermSort.Text, false, 0), (ClassificationTermSort.Files, false, 0), (ClassificationTermSort.Files, true, 0), (ClassificationTermSort.Category, false, 0)]);
        (screen.SortColumn, screen.SortDescending).ShouldBe((TermSortColumn.Category, false));
    }

    [Fact]
    public async Task send_the_trimmed_filter_to_the_daemon()
    {
        var asked = new List<GetClassificationTerms>();
        var (host, screen) = Create(Client(asked));
        using var lifetime = host;
        await screen.RefreshAsync(Token);

        await Ask(asked, () => screen.Filter = " PLACE ");

        asked.Last().Filter.ShouldBe("PLACE");
        screen.Filter.ShouldBe("PLACE");
    }

    [Fact]
    public async Task show_a_page_at_a_time_and_load_the_next_after_those_shown()
    {
        var asked = new List<GetClassificationTerms>();
        var client = Client();
        client.ClassificationTermsReply = query =>
        {
            asked.Add(query);
            var all = Vocabulary().Terms;

            return new Ok<ClassificationTermsView>(new ClassificationTermsView(Vocabulary().Categories, [.. all.Skip(query.Skip).Take(2)], all.Count));
        };
        var (host, screen) = Create(client);
        using var lifetime = host;

        await screen.RefreshAsync(Token);
        var firstPage = (screen.Terms.Count, screen.TermsSummary, screen.CanLoadMoreTerms);
        await screen.LoadTermsAsync(false, Token);

        firstPage.ShouldBe((2, "Showing 2 of 3 terms", true));
        screen.Terms.Select(term => term.Id).ShouldBe([Red, Paris, Blue]);
        asked.Select(query => query.Skip).ShouldBe([0, 2]);
        screen.CanLoadMoreTerms.ShouldBeFalse();
    }

    [Fact]
    public async Task drop_a_reply_that_a_newer_request_has_replaced()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var client = Client();
        client.ClassificationTermsReply = query =>
        {
            if (Interlocked.Increment(ref calls) == 1) release.Task.GetAwaiter().GetResult();

            return new Ok<ClassificationTermsView>(new ClassificationTermsView(Vocabulary().Categories, [.. Vocabulary().Terms.Where(term => term.Text.Contains(query.Filter))], 1));
        };
        var (host, screen) = Create(client);
        using var lifetime = host;
        var slow = Task.Run(() => screen.RefreshAsync(Token), Token);
        while (Volatile.Read(ref calls) == 0) await Task.Delay(5, Token);

        screen.Filter = "paris";
        await Task.Delay(100, Token);
        release.SetResult();
        await slow;

        screen.Terms.Select(term => term.Id).ShouldBe([Paris]);
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
