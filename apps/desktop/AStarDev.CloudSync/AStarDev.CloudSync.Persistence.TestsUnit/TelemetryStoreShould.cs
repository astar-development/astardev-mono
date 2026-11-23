using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class TelemetryStoreShould
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task append_and_read_back_log_entries()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<ITelemetryStore>();
        var first = new TelemetryEntry(TelemetryKind.Log, Noon, 2, "Sync", "Started");
        var second = new TelemetryEntry(TelemetryKind.Log, Noon.AddSeconds(1), 4, "Sync", "Failed badly");
        var span = new TelemetryEntry(TelemetryKind.Span, Noon, 0, "crawl", "12ms");

        await store.AppendAsync([first, second, span], Token);
        var all = await store.QueryAsync(new TelemetryQuery(TelemetryKind.Log), Token);
        var errors = await store.QueryAsync(new TelemetryQuery(TelemetryKind.Log) { MinimumLevel = 3 }, Token);
        var text = await store.QueryAsync(new TelemetryQuery(TelemetryKind.Log) { Text = "STARTED" }, Token);
        var recent = await store.QueryAsync(new TelemetryQuery(TelemetryKind.Log) { Since = Noon.AddSeconds(1), Limit = 1 }, Token);

        all.Value.ShouldBeOfType<Ok<IReadOnlyList<TelemetryEntry>>>().Value.ShouldBe([second, first]);
        errors.Value.ShouldBeOfType<Ok<IReadOnlyList<TelemetryEntry>>>().Value.ShouldBe([second]);
        text.Value.ShouldBeOfType<Ok<IReadOnlyList<TelemetryEntry>>>().Value.ShouldBe([first]);
        recent.Value.ShouldBeOfType<Ok<IReadOnlyList<TelemetryEntry>>>().Value.ShouldBe([second]);
    }

    [Fact]
    public async Task prune_entries_older_than_the_retention()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<ITelemetryStore>();
        var old = new TelemetryEntry(TelemetryKind.Metric, Noon.AddDays(-8), 0, "queue", "1");
        var fresh = new TelemetryEntry(TelemetryKind.Metric, Noon.AddDays(-6), 0, "queue", "2");
        await store.AppendAsync([old, fresh], Token);

        var pruned = await store.PruneAsync(Noon.AddDays(-7), Token);
        var left = await store.QueryAsync(new TelemetryQuery(TelemetryKind.Metric), Token);

        pruned.Value.ShouldBeOfType<Ok<int>>().Value.ShouldBe(1);
        left.Value.ShouldBeOfType<Ok<IReadOnlyList<TelemetryEntry>>>().Value.ShouldBe([fresh]);
    }

    [Fact]
    public async Task return_a_failure_when_the_database_is_unavailable()
    {
        await using var host = PersistenceTestHost.Create(configuration => configuration[$"{TelemetryDatabaseOptions.ConfigurationSection}:Path"] = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "nested", "telemetry.db"));
        var store = host.Services.GetRequiredService<ITelemetryStore>();

        var result = await store.AppendAsync([new TelemetryEntry(TelemetryKind.Log, Noon, 2, "Sync", "x")], Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBeOfType<StorageFailed>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<ITelemetryStore>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => store.AppendAsync([new TelemetryEntry(TelemetryKind.Log, Noon, 2, "Sync", "x")], cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.QueryAsync(new TelemetryQuery(TelemetryKind.Log), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.PruneAsync(Noon, cancelled.Token));
    }

    [Fact]
    public async Task store_queued_entries_as_a_batch_and_count_a_dropped_one()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var batcher = host.Services.GetServices<IHostedService>().OfType<TelemetryBatchWriter>().Single();
        host.Services.GetRequiredService<ITelemetryWriter>().ShouldBeSameAs(batcher);
        var entry = new TelemetryEntry(TelemetryKind.Log, Noon, 2, "Sync", "queued");

        batcher.TryWrite(entry).ShouldBeTrue();
        await batcher.FlushAsync(Token);
        var stored = await host.Services.GetRequiredService<ITelemetryStore>().QueryAsync(new TelemetryQuery(TelemetryKind.Log), Token);

        stored.Value.ShouldBeOfType<Ok<IReadOnlyList<TelemetryEntry>>>().Value.ShouldBe([entry]);
        batcher.DroppedCount.ShouldBe(0);
    }

    [Fact]
    public async Task drop_and_count_a_batch_it_cannot_store()
    {
        await using var host = PersistenceTestHost.Create(configuration => configuration[$"{TelemetryDatabaseOptions.ConfigurationSection}:Path"] = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "nested", "telemetry.db"));
        var batcher = host.Services.GetServices<IHostedService>().OfType<TelemetryBatchWriter>().Single();
        batcher.TryWrite(new TelemetryEntry(TelemetryKind.Log, Noon, 2, "Sync", "a"));
        batcher.TryWrite(new TelemetryEntry(TelemetryKind.Log, Noon, 2, "Sync", "b"));

        await batcher.FlushAsync(Token);

        batcher.DroppedCount.ShouldBe(2);
    }

    [Fact]
    public async Task prune_with_the_saved_retention_through_the_pruning_service()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<ITelemetryStore>();
        var now = TimeProvider.System.GetUtcNow();
        var old = new TelemetryEntry(TelemetryKind.Log, now.AddDays(-8), 2, "Sync", "old");
        var fresh = new TelemetryEntry(TelemetryKind.Log, now.AddDays(-1), 2, "Sync", "fresh");
        await store.AppendAsync([old, fresh], Token);

        await host.Services.GetServices<IHostedService>().OfType<TelemetryPruningService>().Single().PruneOnceAsync(Token);
        var left = await store.QueryAsync(new TelemetryQuery(TelemetryKind.Log), Token);

        left.Value.ShouldBeOfType<Ok<IReadOnlyList<TelemetryEntry>>>().Value.ShouldBe([fresh]);
    }
}
