using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class AuditQueryShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_recorded_entries()
    {
        await using var host = await SeededHostAsync(("ConflictResolved", -1), ("SyncScopeAdded", -2));

        var result = await host.Services.GetRequiredService<AuditQuery>().HandleAsync(Everything([]), Token);

        result.Value.ShouldBeOfType<Ok<AuditView>>().Value.Entries.Select(entry => entry.EventType).ShouldBe(["SyncScopeAdded", "ConflictResolved"]);
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
