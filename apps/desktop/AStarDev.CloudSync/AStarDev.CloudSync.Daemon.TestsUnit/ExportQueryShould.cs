using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ExportQueryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task export_audit_as_csv_with_a_header_and_escaping()
    {
        await using var host = await SeededHostAsync(("ConflictResolved", -1, "{\"choice\":\"KeepLocal\"}"));

        var result = await host.Services.GetRequiredService<ExportQuery>().HandleAsync(new ExportAudit(Everything([])), Token);

        var lines = result.Value.ShouldBeOfType<Ok<ExportView>>().Value.Csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBe(2);
        lines[0].ShouldBe("Id,OccurredUtc,EventType,AggregateId,Details");
        lines[1].ShouldContain(",ConflictResolved,");
        lines[1].ShouldEndWith("\"{\"\"choice\"\":\"\"KeepLocal\"\"}\"");
    }

    [Fact]
    public async Task respect_the_filters()
    {
        await using var host = await SeededHostAsync(("ConflictResolved", -1, "{}"), ("SyncScopeAdded", -2, "{}"));
        var query = host.Services.GetRequiredService<ExportQuery>();
        var audit = host.Services.GetRequiredService<AuditQuery>();
        var filter = Everything(["SyncScopeAdded"]);

        var exported = await query.HandleAsync(new ExportAudit(filter), Token);
        var listed = await audit.HandleAsync(filter, Token);

        var rows = exported.Value.ShouldBeOfType<Ok<ExportView>>().Value.Csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
        rows.Count.ShouldBe(listed.Value.ShouldBeOfType<Ok<AuditView>>().Value.Entries.Count);
        rows.ShouldHaveSingleItem().ShouldContain("SyncScopeAdded");
    }

    [Fact]
    public async Task export_a_header_alone_for_an_empty_history()
    {
        await using var host = DaemonTestHost.Create();
        await host.InitialiseDatabaseAsync(Token);

        var result = await host.Services.GetRequiredService<ExportQuery>().HandleAsync(new ExportHistory(new GetHistory(Status: "Failed")), Token);

        result.Value.ShouldBeOfType<Ok<ExportView>>().Value.Csv.ShouldBe("OperationId,Timestamp,Provider,Type,Status,Path,SizeBytes\r\n");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.Create();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ExportQuery>().HandleAsync(new ExportHistory(new GetHistory()), cancelled.Token));
    }

    private static GetAudit Everything(IReadOnlyList<string> types) => new(DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow.AddDays(1), types);

    private static async Task<DaemonTestHost> SeededHostAsync(params (string Type, int DaysAgo, string Details)[] entries)
    {
        var host = DaemonTestHost.Create();
        await host.InitialiseDatabaseAsync(Token);
        var journal = host.Services.GetRequiredService<IEventJournal>();
        foreach (var (type, daysAgo, details) in entries) await journal.AppendAsync(new JournalEntry(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(daysAgo), type, Guid.NewGuid(), details), Token);

        return host;
    }
}
