using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class StatusBarViewModelShould
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(100);
    private static readonly SyncStatusView Busy = new(ProviderHealthState.Degraded, 7, 2, new DateTimeOffset(2026, 11, 22, 9, 30, 0, TimeSpan.Zero));
    private static readonly CultureService Culture = new("en-GB");
    private static readonly string LastActivityTime = Culture.FormatTime(Busy.LastActivityAt!.Value.ToLocalTime());
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LocalisationService RealLocalisation()
    {
        var strings = LocalisationCatalogue.Load(typeof(LocalisationCatalogue).Assembly, "en-GB").Value.ShouldBeOfType<Ok<IReadOnlyDictionary<string, string>>>().Value;

        return new LocalisationService(Culture, new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en-GB"] = strings }, "en-GB");
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(5, Token);

        condition().ShouldBeTrue();
    }

    [Fact]
    public async Task show_the_daemon_values()
    {
        var client = new FakeIpcClient([], available: true) { SyncStatusReply = new Ok<SyncStatusView>(Busy) };
        var connection = new ConnectionViewModel(() => client, Delay, Delay);
        var bar = new StatusBarViewModel(connection, RealLocalisation(), Culture);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = connection.RunAsync(source.Token);
        await WaitForAsync(() => bar.QueueActivityText.Contains('7'));
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        bar.ConnectionText.ShouldBe("Connected");
        bar.ProviderHealth.Level.ShouldBe(TrustLevel.Degraded);
        bar.ErrorStateText.ShouldContain("2");
        bar.LastActivityText.ShouldContain(LastActivityTime);
    }

    [Fact]
    public async Task follow_status_changes_pushed_by_the_daemon()
    {
        var client = new FakeIpcClient([], available: true) { SyncStatusReply = new Ok<SyncStatusView>(Busy) };
        var connection = new ConnectionViewModel(() => client, Delay, Delay);
        var bar = new StatusBarViewModel(connection, RealLocalisation(), Culture);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = connection.RunAsync(source.Token);
        await WaitForAsync(() => bar.QueueActivityText.Contains('7'));
        client.Raise(new SyncStatusChanged(new SyncStatusView(ProviderHealthState.Healthy, 0, 0, null)));
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        bar.ProviderHealth.Level.ShouldBe(TrustLevel.Healthy);
        bar.QueueActivityText.ShouldNotContain('7');
    }

    [Fact]
    public async Task show_disconnected_when_the_daemon_link_is_down()
    {
        var first = new FakeIpcClient([], available: true) { SyncStatusReply = new Ok<SyncStatusView>(Busy) };
        var queue = new Queue<FakeIpcClient>([first, new FakeIpcClient([], available: false)]);
        var connection = new ConnectionViewModel(() => queue.Count > 1 ? queue.Dequeue() : queue.Peek(), Delay, Delay);
        var bar = new StatusBarViewModel(connection, RealLocalisation(), Culture);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var run = connection.RunAsync(source.Token);
        await WaitForAsync(() => bar.QueueActivityText.Contains('7'));
        first.Drop();
        await WaitForAsync(() => connection.State == ConnectionState.Disconnected && bar.ConnectionText == "Disconnected");
        await source.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => run);

        bar.QueueActivityText.ShouldNotContain('7');
        bar.ErrorStateText.ShouldNotContain('2');
        bar.LastActivityText.ShouldNotContain(LastActivityTime);
        bar.ProviderHealth.Level.ShouldBe(TrustLevel.Critical);
    }
}
