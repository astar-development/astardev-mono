using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels.Operations;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class HistoryViewModelShould
{
    private static readonly DateTimeOffset Reference = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static HistoryEntryView Entry(string type, string status, int daysAgo, string path = "/a.txt", string provider = "OneDrive")
        => new(Guid.NewGuid(), provider, type, status, Reference.AddDays(-daysAgo), path, 10);

    private static FakeIpcClient ClientWith(params HistoryEntryView[] entries)
        => new([], available: true) { HistoryReply = new Ok<HistoryView>(new HistoryView(entries)) };

    [Fact]
    public async Task show_the_daemon_operations()
    {
        var oldest = Entry("DownloadFile", "Completed", 5, "/old.txt");
        var newest = Entry("UploadFile", "Failed", 0, "/new.txt", "Dropbox");
        using var host = OperationScreensHost.Create(ClientWith(oldest, newest));
        var history = host.Screen<HistoryViewModel>();
        history.Rows.ShouldBeEmpty();

        await history.RefreshAsync(Token);

        history.Rows.Select(row => row.Entry.Id).ShouldBe([newest.OperationId.ToString(), oldest.OperationId.ToString()]);
        history.Rows[0].TypeText.ShouldBe("Upload");
        history.Rows[0].Status.Text.ShouldBe("Failed");
        history.Rows[0].Entry.Provider.ShouldBe("Dropbox");
        history.Rows[1].Status.Text.ShouldBe("Completed");
        history.IsSampleData.ShouldBeFalse();
        history.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task show_an_empty_state()
    {
        using var host = OperationScreensHost.Create(ClientWith());
        var history = host.Screen<HistoryViewModel>();

        await history.RefreshAsync(Token);

        history.Rows.ShouldBeEmpty();
        history.HasNoRows.ShouldBeTrue();
        history.NoMatchesText.ShouldBe("No operations to show yet.");
    }

    [Fact]
    public async Task narrow_the_list_by_each_filter()
    {
        var recent = Entry("UploadFile", "Completed", 1, "/Photos/a.jpg");
        var failed = Entry("DeleteLocal", "Failed", 3, "/Docs/b.txt", "Dropbox");
        var old = Entry("DownloadFile", "Completed", 30, "/Photos/c.jpg");
        using var host = OperationScreensHost.Create(ClientWith(recent, failed, old));
        var history = host.Screen<HistoryViewModel>();
        await history.RefreshAsync(Token);

        history.SelectedStatus = history.StatusChoices.Single(choice => choice.Value == nameof(OperationStatus.Failed));
        history.Rows.Select(row => row.Entry.Id).ShouldBe([failed.OperationId.ToString()]);
        history.ClearFilters.Execute(null);

        history.SelectedProvider = history.ProviderChoices.Single(choice => choice.Value == "Dropbox");
        history.Rows.Select(row => row.Entry.Id).ShouldBe([failed.OperationId.ToString()]);
        history.ClearFilters.Execute(null);

        history.SelectedType = history.TypeChoices.Single(choice => choice.Value == nameof(OperationType.Download));
        history.Rows.Select(row => row.Entry.Id).ShouldBe([old.OperationId.ToString()]);
        history.ClearFilters.Execute(null);

        history.DateFrom = new DateTime(2026, 11, 15);
        history.DateTo = new DateTime(2026, 11, 19);
        history.Rows.Select(row => row.Entry.Id).ShouldBe([recent.OperationId.ToString(), failed.OperationId.ToString()]);
        history.ClearFilters.Execute(null);

        history.PathText = "photos";
        history.Rows.Select(row => row.Entry.Id).ShouldBe([recent.OperationId.ToString(), old.OperationId.ToString()]);
        history.NoMatchesText.ShouldBe("No operations match the filters.");
    }

    [Fact]
    public async Task keep_the_chosen_provider_when_it_reloads()
    {
        var client = ClientWith(Entry("UploadFile", "Completed", 1, provider: "Dropbox"), Entry("UploadFile", "Completed", 2));
        using var host = OperationScreensHost.Create(client);
        var history = host.Screen<HistoryViewModel>();
        await history.RefreshAsync(Token);
        history.SelectedProvider = history.ProviderChoices.Single(choice => choice.Value == "Dropbox");

        await history.RefreshAsync(Token);

        history.SelectedProvider.Value.ShouldBe("Dropbox");
        history.Rows.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task keep_the_list_and_show_the_reason_when_the_daemon_cannot_answer()
    {
        var client = ClientWith(Entry("UploadFile", "Completed", 1));
        using var host = OperationScreensHost.Create(client);
        var history = host.Screen<HistoryViewModel>();
        await history.RefreshAsync(Token);
        client.HistoryReply = new Fail<IpcError>(new HandlerFailed("The operation history could not be read."));

        await history.RefreshAsync(Token);

        history.ErrorMessage.ShouldBe("The operation history could not be read.");
        history.Rows.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task open_the_operation_of_a_deep_link_once_loaded()
    {
        var target = Entry("UploadFile", "Completed", 1);
        using var host = OperationScreensHost.Create(ClientWith(target, Entry("UploadFile", "Completed", 2)));
        var history = host.Screen<HistoryViewModel>();

        history.OnNavigatedTo(target.OperationId.ToString());
        await Task.Run(async () =>
        {
            while (history.Details.Row is null) await Task.Delay(10, Token);
        }, Token).WaitAsync(TimeSpan.FromSeconds(5), Token);

        history.Details.Row.ShouldNotBeNull().Entry.Id.ShouldBe(target.OperationId.ToString());
    }

    [Fact]
    public async Task honour_cancellation()
    {
        using var host = OperationScreensHost.Create(ClientWith());
        var history = host.Screen<HistoryViewModel>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => history.RefreshAsync(cancelled.Token));
    }

    [Fact]
    public void not_offer_an_audit_tab()
    {
        using var host = OperationScreensHost.Create();

        var history = host.Screen<HistoryViewModel>();

        typeof(HistoryViewModel).GetProperties().Select(property => property.Name).Intersect(["TabIndex", "AuditTabText", "AuditPendingText"]).ShouldBeEmpty();
        history.Title.ShouldNotStartWith("!");
    }
}
