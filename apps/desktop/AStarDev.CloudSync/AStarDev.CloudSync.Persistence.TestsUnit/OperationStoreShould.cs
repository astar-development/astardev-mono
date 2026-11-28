using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class OperationStoreShould
{
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_only_the_newest_operations_in_the_asked_statuses()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = (await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge)).Id;
        var store = host.Services.GetRequiredService<IOperationStore>();
        var oldPending = NewOperation(job, OperationStatus.Pending, Start);
        var running = NewOperation(job, OperationStatus.Running, Start.AddMinutes(1));
        var newPending = NewOperation(job, OperationStatus.Pending, Start.AddMinutes(2));
        var completed = NewOperation(job, OperationStatus.Completed, Start.AddMinutes(3));
        await EnqueueAsync(store, oldPending, running, newPending, completed);

        var listed = await store.ListNewestAsync([OperationStatus.Pending, OperationStatus.Running], 2, Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value.Select(operation => operation.Id).ShouldBe([newPending.Id, running.Id]);
    }

    [Fact]
    public async Task count_the_operations_of_each_job_by_status()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = (await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge)).Id;
        var store = host.Services.GetRequiredService<IOperationStore>();
        await EnqueueAsync(store, NewOperation(job, OperationStatus.Pending, Start), NewOperation(job, OperationStatus.Pending, Start), NewOperation(job, OperationStatus.Completed, Start));

        var counts = await store.CountByJobAndStatusAsync(Token);

        var ofJob = counts.Value.ShouldBeOfType<Ok<IReadOnlyDictionary<SyncJobId, IReadOnlyDictionary<OperationStatus, int>>>>().Value[job];
        ofJob[OperationStatus.Pending].ShouldBe(2);
        ofJob[OperationStatus.Completed].ShouldBe(1);
        ofJob.ContainsKey(OperationStatus.Running).ShouldBeFalse();
    }

    [Fact]
    public async Task prune_only_completed_operations_created_before_the_cutoff()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = (await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge)).Id;
        var store = host.Services.GetRequiredService<IOperationStore>();
        var oldCompleted = NewOperation(job, OperationStatus.Completed, Start);
        var oldFailed = NewOperation(job, OperationStatus.DeadLetter, Start);
        var oldPending = NewOperation(job, OperationStatus.Pending, Start);
        var freshCompleted = NewOperation(job, OperationStatus.Completed, Start.AddDays(2));
        await EnqueueAsync(store, oldCompleted, oldFailed, oldPending, freshCompleted);

        var pruned = await store.PruneCompletedAsync(Start.AddDays(1), Token);
        var left = (await store.ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;

        pruned.Value.ShouldBeOfType<Ok<int>>().Value.ShouldBe(1);
        left.Select(operation => operation.Id).ShouldBe([oldFailed.Id, oldPending.Id, freshCompleted.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task prune_completed_operations_with_the_saved_retention_through_the_pruning_service()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = (await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge)).Id;
        var store = host.Services.GetRequiredService<IOperationStore>();
        var now = TimeProvider.System.GetUtcNow();
        var old = NewOperation(job, OperationStatus.Completed, now.AddDays(-31));
        var fresh = NewOperation(job, OperationStatus.Completed, now.AddDays(-1));
        await EnqueueAsync(store, old, fresh);

        await host.Services.GetServices<IHostedService>().OfType<OperationPruningService>().Single().PruneOnceAsync(Token);
        var left = (await store.ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;

        left.Select(operation => operation.Id).ShouldBe([fresh.Id]);
    }

    [Fact]
    public async Task honour_cancellation_of_the_filtered_reads_and_the_prune()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<IOperationStore>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => store.ListNewestAsync([OperationStatus.Pending], 1, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.CountByJobAndStatusAsync(cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.PruneCompletedAsync(Start, cancelled.Token));
    }

    private static Operation NewOperation(SyncJobId job, OperationStatus status, DateTimeOffset createdUtc)
        => new(new OperationId(Guid.NewGuid()), job, new ItemId(Guid.NewGuid()), OperationType.UploadFile, OperationPriority.Background, status, 0, createdUtc, null, createdUtc);

    private static async Task EnqueueAsync(IOperationStore store, params Operation[] operations)
    {
        foreach (var operation in operations) (await store.EnqueueAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();
    }
}
