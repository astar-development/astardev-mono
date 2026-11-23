using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class LogsViewModelShould
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static (Microsoft.Extensions.Hosting.IHost Host, LogsViewModel Screen) Create(FakeIpcClient client)
    {
        var host = OperationScreensHost.Create(client, new FixedTimeProvider(Now));

        return (host, host.Screen<LogsViewModel>());
    }

    [Fact]
    public async Task show_the_entries_from_the_daemon()
    {
        var client = new FakeIpcClient([], available: true) { LogsReply = _ => new Ok<LogsView>(new LogsView([new LogEntryView(Now, 3, "Sync", "Queued 3 operations"), new LogEntryView(Now.AddMinutes(-1), 2, "IPC", "Opened a connection")])) };
        var (host, screen) = Create(client);
        using var lifetime = host;

        await screen.RefreshAsync(Token);

        screen.Entries.Select(row => row.Message).ShouldBe(["Queued 3 operations", "Opened a connection"]);
        screen.Entries[0].Header.ShouldContain("Warning");
        screen.Entries[0].Header.ShouldContain("Sync");
        screen.IsEmpty.ShouldBeFalse();
        screen.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task send_the_filters_to_the_daemon()
    {
        GetLogs? asked = null;
        var client = new FakeIpcClient([], available: true) { LogsReply = query => { asked = query; return new Ok<LogsView>(new LogsView([])); } };
        var (host, screen) = Create(client);
        using var lifetime = host;
        screen.MinimumLevel = 3;
        screen.WindowIndex = 1;
        screen.Text = "queue";

        await screen.RefreshAsync(Token);

        asked.ShouldBe(new GetLogs(3, Now.AddHours(-1), "queue"));
        screen.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task keep_the_entries_and_say_why_when_the_daemon_is_unreachable()
    {
        var reachable = new FakeIpcClient([], available: true) { LogsReply = _ => new Ok<LogsView>(new LogsView([new LogEntryView(Now, 2, "Sync", "Kept")])) };
        var (host, screen) = Create(reachable);
        using var lifetime = host;
        await screen.RefreshAsync(Token);
        reachable.LogsReply = _ => new Fail<IpcError>(new HandlerFailed("The logs could not be read because the database failed."));

        await screen.RefreshAsync(Token);

        screen.Entries.ShouldHaveSingleItem().Message.ShouldBe("Kept");
        screen.ErrorMessage.ShouldBe("The logs could not be read because the database failed.");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var (host, screen) = Create(new FakeIpcClient([], available: true));
        using var lifetime = host;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => screen.RefreshAsync(cancelled.Token));
    }
}
