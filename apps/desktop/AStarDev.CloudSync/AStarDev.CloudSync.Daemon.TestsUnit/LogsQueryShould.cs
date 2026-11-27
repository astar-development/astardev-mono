using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class LogsQueryShould
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task filter_by_level_and_time()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);
        var store = host.Services.GetRequiredService<ITelemetryStore>();
        await store.AppendAsync(
            [
                new TelemetryEntry(TelemetryKind.Log, Noon.AddHours(-2), 2, "Sync", "Old information"),
                new TelemetryEntry(TelemetryKind.Log, Noon, 2, "Sync", "Recent information"),
                new TelemetryEntry(TelemetryKind.Log, Noon.AddMinutes(1), 4, "Sync", "Recent error"),
                new TelemetryEntry(TelemetryKind.Span, Noon, 0, "crawl", "not a log")
            ],
            Token);
        var query = host.Services.GetRequiredService<LogsQuery>();

        var all = await query.HandleAsync(new GetLogs(), Token);
        var errors = await query.HandleAsync(new GetLogs(MinimumLevel: 3), Token);
        var recent = await query.HandleAsync(new GetLogs(Since: Noon.AddMinutes(-5)), Token);
        var text = await query.HandleAsync(new GetLogs(Text: "OLD"), Token);

        all.Value.ShouldBeOfType<Ok<LogsView>>().Value.Entries.Select(entry => entry.Message).ShouldBe(["Recent error", "Recent information", "Old information"]);
        errors.Value.ShouldBeOfType<Ok<LogsView>>().Value.Entries.ShouldBe([new LogEntryView(Noon.AddMinutes(1), 4, "Sync", "Recent error")]);
        recent.Value.ShouldBeOfType<Ok<LogsView>>().Value.Entries.Select(entry => entry.Message).ShouldBe(["Recent error", "Recent information"]);
        text.Value.ShouldBeOfType<Ok<LogsView>>().Value.Entries.Select(entry => entry.Message).ShouldBe(["Old information"]);
    }

    [Fact]
    public async Task cut_a_long_message_so_the_answer_fits_an_ipc_frame()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);
        await host.Services.GetRequiredService<ITelemetryStore>().AppendAsync([new TelemetryEntry(TelemetryKind.Log, Noon, 2, "Sync", new string('x', ApplicationConstants.LogMessageLimit + 50))], Token);

        var result = await host.Services.GetRequiredService<LogsQuery>().HandleAsync(new GetLogs(), Token);

        result.Value.ShouldBeOfType<Ok<LogsView>>().Value.Entries.Single().Message.Length.ShouldBe(ApplicationConstants.LogMessageLimit);
    }

    [Fact]
    public async Task fail_with_a_message_when_the_store_fails()
    {
        await using var host = DaemonTestHost.Create(configureServices: services => services.RemoveAll<ITelemetryStore>().AddSingleton<ITelemetryStore, FailingTelemetryStore>());

        var result = await host.Services.GetRequiredService<LogsQuery>().HandleAsync(new GetLogs(), Token);

        var error = result.Value.ShouldBeOfType<Fail<IpcError>>().Error.Value.ShouldBeOfType<HandlerFailed>();
        error.Message.ShouldContain("logs could not be read");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<LogsQuery>().HandleAsync(new GetLogs(), cancelled.Token));
    }
}
