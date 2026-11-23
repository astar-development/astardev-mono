using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class OperationDetailsViewModelShould
{
    private static readonly DateTimeOffset Reference = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FakeIpcClient ClientWithOneEntry()
        => new([], available: true) { HistoryReply = new Ok<HistoryView>(new HistoryView([new HistoryEntryView(Guid.NewGuid(), "OneDrive", "UploadFile", "Completed", Reference, "/a.txt", 10)])) };

    [Fact]
    public async Task open_without_navigating_away()
    {
        using var host = OperationScreensHost.Create(ClientWithOneEntry());
        var navigation = host.Services.GetRequiredService<INavigationService>();
        _ = navigation.Navigate(ScreenId.History);
        var raised = 0;
        navigation.Navigated += (_, _) => raised++;
        var history = host.Screen<HistoryViewModel>();
        await history.RefreshAsync(Token);

        history.Details.Open(history.Rows[0]);

        history.Details.IsOpen.ShouldBeTrue();
        history.Details.Row.ShouldBeSameAs(history.Rows[0]);
        navigation.Current.ShouldBeSameAs(history);
        raised.ShouldBe(0);
    }

    [Fact]
    public async Task close_and_stay_closed_when_closed_twice()
    {
        using var host = OperationScreensHost.Create(ClientWithOneEntry());
        var history = host.Screen<HistoryViewModel>();
        await history.RefreshAsync(Token);
        history.Details.Open(history.Rows[0]);

        history.Details.Close();
        history.Details.Close();

        history.Details.IsOpen.ShouldBeFalse();
        history.Details.Row.ShouldBeNull();
    }
}
