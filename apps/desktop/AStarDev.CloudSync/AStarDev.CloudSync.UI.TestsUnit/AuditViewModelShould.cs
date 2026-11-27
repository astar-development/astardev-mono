using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class AuditViewModelShould
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static (Microsoft.Extensions.Hosting.IHost Host, AuditViewModel Screen) Create(FakeIpcClient client)
    {
        var host = OperationScreensHost.Create(client, new FixedTimeProvider(Now));

        return (host, host.Screen<AuditViewModel>());
    }

    [Fact]
    public async Task show_the_entries_from_the_daemon()
    {
        var older = new AuditEntryView(Guid.NewGuid(), Now.AddHours(-2), "SyncScopeAdded", Guid.NewGuid(), "{}");
        var newer = new AuditEntryView(Guid.NewGuid(), Now.AddMinutes(-5), "ConflictResolved", Guid.NewGuid(), "{\"choice\":\"KeepLocal\"}");
        GetAudit? asked = null;
        var client = new FakeIpcClient([], available: true) { AuditReply = query => { asked = query; return new Ok<AuditView>(new AuditView(2, [newer, older])); } };
        var (host, screen) = Create(client);
        using var lifetime = host;

        await screen.RefreshAsync(Token);

        screen.Entries.Select(row => row.Message).ShouldBe(["{\"choice\":\"KeepLocal\"}", "{}"]);
        screen.Entries[0].Header.ShouldContain("ConflictResolved");
        screen.IsEmpty.ShouldBeFalse();
        screen.ErrorMessage.ShouldBeNull();
        asked.ShouldNotBeNull().EventTypes.ShouldBeEmpty();
        asked.ToUtc.ShouldBeGreaterThanOrEqualTo(Now);
    }

    [Fact]
    public async Task show_an_empty_state()
    {
        var (host, screen) = Create(new FakeIpcClient([], available: true));
        using var lifetime = host;

        await screen.RefreshAsync(Token);

        screen.Entries.ShouldBeEmpty();
        screen.IsEmpty.ShouldBeTrue();
        screen.EmptyText.ShouldNotStartWith("!");
    }

    [Fact]
    public async Task keep_the_entries_and_say_why_when_the_daemon_fails()
    {
        var reachable = new FakeIpcClient([], available: true) { AuditReply = _ => new Ok<AuditView>(new AuditView(1, [new AuditEntryView(Guid.NewGuid(), Now, "SyncScopeAdded", Guid.NewGuid(), "{}")])) };
        var (host, screen) = Create(reachable);
        using var lifetime = host;
        await screen.RefreshAsync(Token);
        reachable.AuditReply = _ => new Fail<IpcError>(new HandlerFailed("The audit trail could not be read because the database failed."));

        await screen.RefreshAsync(Token);

        screen.Entries.ShouldHaveSingleItem();
        screen.ErrorMessage.ShouldBe("The audit trail could not be read because the database failed.");
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

    [Fact]
    public async Task load_older_entries_below_those_shown_until_none_remain()
    {
        var entries = Enumerable.Range(0, 3).Select(index => new AuditEntryView(Guid.NewGuid(), Now.AddMinutes(-index), "SyncScopeAdded", Guid.NewGuid(), $"{{\"n\":{index}}}")).ToList();
        var asked = new List<GetAudit>();
        var client = new FakeIpcClient([], available: true) { AuditReply = query => { asked.Add(query); return new Ok<AuditView>(new AuditView(3, [.. entries.Skip(query.Skip).Take(2)])); } };
        var (host, screen) = Create(client);
        using var lifetime = host;
        await screen.RefreshAsync(Token);
        screen.CanLoadMore.ShouldBeTrue();

        await screen.LoadMoreCommand.ExecuteAsync(null);

        asked.Select(query => query.Skip).ShouldBe([0, 2]);
        screen.Entries.Select(row => row.Message).ShouldBe(["{\"n\":0}", "{\"n\":1}", "{\"n\":2}"]);
        screen.CanLoadMore.ShouldBeFalse();
    }

    [Fact]
    public async Task honour_cancellation_when_loading_more()
    {
        var (host, screen) = Create(new FakeIpcClient([], available: true));
        using var lifetime = host;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => screen.LoadAsync(false, cancelled.Token));
    }
}
