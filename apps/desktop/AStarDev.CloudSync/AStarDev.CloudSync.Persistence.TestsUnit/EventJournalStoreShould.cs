using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var journal = host.Services.GetRequiredService<IEventJournal>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => journal.ListAsync(Stamp, Stamp, [], cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => journal.PruneAsync(Stamp, cancelled.Token));
    }

    private static JournalEntry Entry(DateTimeOffset occurred, string type) => new(Guid.NewGuid(), occurred, type, Guid.NewGuid(), "{}");
}
