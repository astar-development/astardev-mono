using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.ViewModels;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class ExportCommandShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IHost Create(FakeIpcClient client, string? chosen) => OperationScreensHost.Create(client, filePicker: new FakeFilePicker(chosen));

    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"cloudsync-export-{Guid.NewGuid():N}.csv");

    [Fact]
    public async Task write_the_file_the_user_chose()
    {
        var path = TempFile();
        var client = new FakeIpcClient([], available: true) { ExportReply = _ => new Ok<ExportView>(new ExportView("a,b\r\n")) };
        using var host = Create(client, path);

        var result = await host.Services.GetRequiredService<ExportCommand>().RunAsync(new ExportHistory(new GetHistory()), "history.csv", Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        (await File.ReadAllTextAsync(path, Token)).ShouldBe("a,b\r\n");
        File.Delete(path);
    }

    [Fact]
    public async Task write_nothing_when_the_user_dismisses_the_dialog()
    {
        var asked = false;
        var client = new FakeIpcClient([], available: true) { ExportReply = _ => { asked = true; return new Ok<ExportView>(new ExportView("x")); } };
        using var host = Create(client, null);

        var result = await host.Services.GetRequiredService<ExportCommand>().RunAsync(new ExportAudit(new GetAudit(DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow, [])), "audit.csv", Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        asked.ShouldBeFalse();
    }

    [Fact]
    public async Task show_an_error_when_the_save_fails()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "history.csv");
        using var host = Create(new FakeIpcClient([], available: true), missing);
        var screen = host.Screen<HistoryViewModel>();

        await screen.ExportAsync(Token);

        screen.ErrorMessage.ShouldNotBeNull().ShouldContain(missing);
        File.Exists(missing).ShouldBeFalse();
    }

    [Fact]
    public async Task show_the_daemon_failure_on_the_audit_screen()
    {
        var client = new FakeIpcClient([], available: true) { ExportReply = _ => new Fail<IpcError>(new HandlerFailed("The audit trail could not be read because the database failed.")) };
        using var host = Create(client, TempFile());
        var screen = host.Screen<AuditViewModel>();

        await screen.ExportAsync(Token);

        screen.ErrorMessage.ShouldBe("The audit trail could not be read because the database failed.");
    }

    [Fact]
    public async Task export_with_the_filters_the_history_screen_shows()
    {
        var path = TempFile();
        IQuery<ExportView>? asked = null;
        var client = new FakeIpcClient([], available: true) { ExportReply = query => { asked = query; return new Ok<ExportView>(new ExportView("h\r\n")); } };
        using var host = Create(client, path);
        var screen = host.Screen<HistoryViewModel>();
        screen.PathText = "reports";
        screen.DateFrom = new DateTime(2026, 11, 20);
        screen.DateTo = new DateTime(2026, 11, 21);

        await screen.ExportAsync(Token);

        var filter = asked.ShouldBeOfType<ExportHistory>().Filter;
        filter.Path.ShouldBe("reports");
        filter.From.ShouldBe(new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero));
        filter.To.ShouldBe(new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.Zero).AddTicks(-1));
        screen.ErrorMessage.ShouldBeNull();
        File.Delete(path);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        using var host = Create(new FakeIpcClient([], available: true), TempFile());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ExportCommand>().RunAsync(new ExportHistory(new GetHistory()), "history.csv", cancelled.Token));
    }
}
