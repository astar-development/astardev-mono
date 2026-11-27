using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class AuditQueryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_recorded_entries_newest_first()
    {
        await using var host = await SeededHostAsync(("ConflictResolved", -1), ("SyncScopeAdded", -2));

        var result = await host.Services.GetRequiredService<AuditQuery>().HandleAsync(Everything([]), Token);

        result.Value.ShouldBeOfType<Ok<AuditView>>().Value.Entries.Select(entry => entry.EventType).ShouldBe(["ConflictResolved", "SyncScopeAdded"]);
    }

    [Fact]
    public async Task list_at_most_one_page_newest_first_with_the_total_so_the_answer_fits_an_ipc_frame()
    {
        var total = ApplicationConstants.ListLimit + 5;
        await using var host = await SeededHostAsync([.. Enumerable.Range(0, total).Select(index => ("ConflictResolved", -1 - (index % 80)))]);
        var query = host.Services.GetRequiredService<AuditQuery>();

        var first = (await query.HandleAsync(Everything([]) with { Take = 10_000 }, Token)).Value.ShouldBeOfType<Ok<AuditView>>().Value;
        var rest = (await query.HandleAsync(Everything([]) with { Skip = ApplicationConstants.ListLimit }, Token)).Value.ShouldBeOfType<Ok<AuditView>>().Value;

        (first.Total, first.Entries.Count, rest.Entries.Count).ShouldBe((total, ApplicationConstants.ListLimit, 5));
        first.Entries.Concat(rest.Entries).Select(entry => entry.OccurredUtc).ShouldBeInOrder(SortDirection.Descending);
        first.Entries.Concat(rest.Entries).Select(entry => entry.Id).Distinct().Count().ShouldBe(total);
    }

    [Fact]
    public async Task export_every_entry_without_the_screen_limit()
    {
        var total = ApplicationConstants.ListLimit + 5;
        await using var host = await SeededHostAsync([.. Enumerable.Range(0, total).Select(index => ("ConflictResolved", -1 - (index % 80)))]);

        var result = await host.Services.GetRequiredService<ExportQuery>().HandleAsync(new ExportAudit(Everything([])), Token);

        result.Value.ShouldBeOfType<Ok<ExportView>>().Value.Csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(total + 1);
    }

    [Fact]
    public async Task export_newest_first_like_the_screen()
    {
        await using var host = await SeededHostAsync(("SyncScopeAdded", -2), ("ConflictResolved", -1));

        var result = await host.Services.GetRequiredService<ExportQuery>().HandleAsync(new ExportAudit(Everything([])), Token);

        var rows = result.Value.ShouldBeOfType<Ok<ExportView>>().Value.Csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1);
        rows.Select(row => row.Split(',')[2]).ShouldBe(["ConflictResolved", "SyncScopeAdded"]);
    }

    [Fact]
    public async Task filter_by_type()
    {
        await using var host = await SeededHostAsync(("ConflictResolved", -1), ("SyncScopeAdded", -2));

        var result = await host.Services.GetRequiredService<AuditQuery>().HandleAsync(Everything(["SyncScopeAdded"]), Token);

        result.Value.ShouldBeOfType<Ok<AuditView>>().Value.Entries.ShouldHaveSingleItem().EventType.ShouldBe("SyncScopeAdded");
    }

    [Fact]
    public async Task prune_entries_older_than_the_retention()
    {
        await using var host = await SeededHostAsync(("ConflictResolved", -1), ("ConflictDetected", -91));

        var result = await host.Services.GetRequiredService<AuditQuery>().HandleAsync(Everything([]), Token);

        result.Value.ShouldBeOfType<Ok<AuditView>>().Value.Entries.ShouldHaveSingleItem().EventType.ShouldBe("ConflictResolved");
    }

    [Fact]
    public async Task honour_the_retention_setting()
    {
        await using var host = await SeededHostAsync(("ConflictResolved", -1), ("ConflictDetected", -10));
        await host.Services.GetRequiredService<ISettingsStore>().SetAsync(SettingNames.AuditRetentionDays, "5", Token);

        var result = await host.Services.GetRequiredService<AuditQuery>().HandleAsync(Everything([]), Token);

        result.Value.ShouldBeOfType<Ok<AuditView>>().Value.Entries.ShouldHaveSingleItem().EventType.ShouldBe("ConflictResolved");
    }

    [Fact]
    public async Task answer_over_ipc()
    {
        await using var host = await SeededHostAsync(("ConflictResolved", -1));
        await using var client = UnixSocketIpc.CreateClient(host.SocketPath);
        await host.StartAsync(Token);
        (await client.ConnectAsync(Token)).Value.ShouldBeOfType<Ok<Unit>>();

        var audit = await client.QueryAsync(Everything([]), Token);
        await host.StopAsync(Token);

        audit.Value.ShouldBeOfType<Ok<AuditView>>().Value.Entries.ShouldHaveSingleItem().EventType.ShouldBe("ConflictResolved");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.Create();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<AuditQuery>().HandleAsync(Everything([]), cancelled.Token));
    }

    private static GetAudit Everything(IReadOnlyList<string> types) => new(DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow.AddDays(1), types);

    private static async Task<DaemonTestHost> SeededHostAsync(params (string Type, int DaysAgo)[] entries)
    {
        var host = DaemonTestHost.Create();
        await host.InitialiseDatabaseAsync(Token);
        var journal = host.Services.GetRequiredService<IEventJournal>();
        foreach (var (type, daysAgo) in entries) await journal.AppendAsync(new JournalEntry(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(daysAgo), type, Guid.NewGuid(), "{}"), Token);

        return host;
    }
}
