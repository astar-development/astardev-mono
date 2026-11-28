using System.Data.Common;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class EventJournalStoreShould
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_entries_in_a_time_range()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var journal = host.Services.GetRequiredService<IEventJournal>();
        var before = Entry(Stamp.AddDays(-2), "ConflictDetected");
        var first = Entry(Stamp, "ConflictResolved");
        var last = Entry(Stamp.AddHours(1), "SyncScopeAdded");
        var after = Entry(Stamp.AddDays(2), "ConflictDetected");
        foreach (var entry in new[] { last, after, before, first }) await journal.AppendAsync(entry, Token);

        var result = await journal.ListAsync(Stamp, Stamp.AddHours(1), [], Token);

        result.Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value.ShouldBe([first, last]);
    }

    [Fact]
    public async Task list_only_the_requested_types()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var journal = host.Services.GetRequiredService<IEventJournal>();
        var resolved = Entry(Stamp, "ConflictResolved");
        await journal.AppendAsync(resolved, Token);
        await journal.AppendAsync(Entry(Stamp, "SyncScopeAdded"), Token);

        var result = await journal.ListAsync(Stamp.AddDays(-1), Stamp.AddDays(1), ["ConflictResolved"], Token);

        result.Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value.ShouldBe([resolved]);
    }

    [Fact]
    public async Task prune_entries_older_than_the_retention()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var journal = host.Services.GetRequiredService<IEventJournal>();
        var kept = Entry(Stamp, "ConflictResolved");
        await journal.AppendAsync(Entry(Stamp.AddDays(-91), "ConflictDetected"), Token);
        await journal.AppendAsync(kept, Token);

        var pruned = await journal.PruneAsync(Stamp.AddDays(-90), Token);
        var remaining = await journal.ListAsync(Stamp.AddYears(-1), Stamp.AddYears(1), [], Token);

        pruned.Value.ShouldBeOfType<Ok<int>>().Value.ShouldBe(1);
        remaining.Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value.ShouldBe([kept]);
    }

    [Fact]
    public async Task list_by_instant_when_the_bounds_carry_an_offset()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var journal = host.Services.GetRequiredService<IEventJournal>();
        var inside = Entry(Stamp, "ConflictResolved");
        await journal.AppendAsync(inside, Token);
        await journal.AppendAsync(Entry(Stamp.AddHours(3), "ConflictResolved"), Token);
        var offset = TimeSpan.FromHours(2);

        var result = await journal.ListAsync(Stamp.ToOffset(offset), Stamp.AddHours(1).ToOffset(offset), [], Token);

        result.Value.ShouldBeOfType<Ok<IReadOnlyList<JournalEntry>>>().Value.ShouldBe([inside]);
    }

    [Fact]
    public async Task filter_the_time_range_and_the_types_in_sql()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        await using var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(Token);

        var sql = EventJournalStore.Matching(context, Stamp, Stamp.AddDays(1), ["SyncScopeAdded", "SyncScopeRemoved"]).ToQueryString();

        sql.ShouldContain("\"e\".\"OccurredUtc\" >= @from AND \"e\".\"OccurredUtc\" <= @to");
        sql.ShouldContain("\"e\".\"EventType\" IN (@eventTypes1, @eventTypes2)");
    }

    [Fact]
    public async Task find_the_requested_types_through_the_event_type_index()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        await using var context = await host.Services.GetRequiredService<IDbContextFactory<CloudSyncDbContext>>().CreateDbContextAsync(Token);
        await using var command = EventJournalStore.Matching(context, DateTimeOffset.UnixEpoch, Stamp, ["SyncScopeAdded", "SyncScopeRemoved"]).CreateDbCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + command.CommandText;
        await context.Database.OpenConnectionAsync(Token);

        var plan = await QueryPlanAsync(command);

        plan.ShouldBe(["SEARCH e USING INDEX IX_EventJournal_EventType_OccurredUtc (EventType=? AND OccurredUtc>? AND OccurredUtc<?)"]);
    }

    [Fact]
    public async Task prune_with_one_delete_statement()
    {
        var logs = new EventCapturingLoggerProvider();
        await using var host = PersistenceTestHost.Create(configureServices: services => services.AddSingleton<ILoggerProvider>(logs));
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var journal = host.Services.GetRequiredService<IEventJournal>();
        await journal.AppendAsync(Entry(Stamp.AddDays(-91), "ConflictDetected"), Token);
        await journal.AppendAsync(Entry(Stamp, "ConflictResolved"), Token);
        logs.Entries.Clear();

        var pruned = await journal.PruneAsync(Stamp.AddDays(-90), Token);

        pruned.Value.ShouldBeOfType<Ok<int>>().Value.ShouldBe(1);
        logs.Entries.Count(entry => entry.EventId == RelationalEventId.CommandExecuted.Id).ShouldBe(1);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var journal = host.Services.GetRequiredService<IEventJournal>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => journal.ListAsync(Stamp, Stamp, [], cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => journal.ListAsync(Stamp, Stamp, ["ConflictResolved"], cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => journal.PageAsync(Stamp, Stamp, [], 0, 1, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => journal.PruneAsync(Stamp, cancelled.Token));
    }

    private static async Task<List<string>> QueryPlanAsync(DbCommand command)
    {
        var plan = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(Token);
        while (await reader.ReadAsync(Token))
            plan.Add(reader.GetString(3));

        return plan;
    }

    private static JournalEntry Entry(DateTimeOffset occurred, string type) => new(Guid.NewGuid(), occurred, type, Guid.NewGuid(), "{}");
}
