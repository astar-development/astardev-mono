using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class OperationQueueShould
{
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task never_run_two_operations_for_one_item_at_once()
    {
        await using var host = DatabaseTestHost.Create();
        var job = await SeedJobAsync(host);
        var store = host.Services.GetRequiredService<IOperationStore>();
        var item = new ItemId(Guid.NewGuid());
        var first = NewOperation(job, item, OperationPriority.Background, 0);
        var second = NewOperation(job, item, OperationPriority.Background, 1);
        await EnqueueAsync(store, first, second);

        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.ClaimNextAsync(Start.AddHours(1), 5, Token)));
        var claimed = claims.SelectMany(Claimed).ToList();
        var whileRunning = Claimed(await store.ClaimNextAsync(Start.AddHours(1), 5, Token));
        (await store.CompleteAsync(first.Id, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var afterComplete = Claimed(await store.ClaimNextAsync(Start.AddHours(1), 5, Token));

        claimed.Select(operation => operation.Id).ShouldBe([first.Id]);
        whileRunning.ShouldBeEmpty();
        afterComplete.Select(operation => operation.Id).ShouldBe([second.Id]);
    }

    [Fact]
    public async Task run_operations_for_different_items_together()
    {
        await using var host = DatabaseTestHost.Create();
        var job = await SeedJobAsync(host);
        var store = host.Services.GetRequiredService<IOperationStore>();
        var first = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.Background, 0);
        var second = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.Background, 1);
        await EnqueueAsync(store, first, second);

        var claimed = Claimed(await store.ClaimNextAsync(Start.AddHours(1), 5, Token));

        claimed.Select(operation => operation.Id).ShouldBe([first.Id, second.Id]);
        claimed.ShouldAllBe(operation => operation.Status == OperationStatus.Running && operation.Attempts == 1);
    }

    [Fact]
    public async Task claim_by_priority_then_age()
    {
        await using var host = DatabaseTestHost.Create();
        var job = await SeedJobAsync(host);
        var store = host.Services.GetRequiredService<IOperationStore>();
        var oldBackground = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.Background, 0);
        var newBackground = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.Background, 2);
        var maintenance = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.Maintenance, 1);
        var user = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.UserInitiated, 3);
        await EnqueueAsync(store, oldBackground, newBackground, maintenance, user);

        var claimed = Claimed(await store.ClaimNextAsync(Start.AddHours(1), 10, Token));
        var listed = Claimed(await store.ListAsync(Token));

        claimed.Select(operation => operation.Id).ShouldBe([user.Id, oldBackground.Id, newBackground.Id, maintenance.Id]);
        listed.Count.ShouldBe(4);
    }

    [Fact]
    public async Task keep_operations_across_a_restart()
    {
        await using var host = DatabaseTestHost.Create();
        var job = await SeedJobAsync(host);
        var store = host.Services.GetRequiredService<IOperationStore>();
        var pending = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.Background, 0);
        var running = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.UserInitiated, 1);
        await EnqueueAsync(store, pending, running);
        Claimed(await store.ClaimNextAsync(Start.AddHours(1), 1, Token)).Single().Id.ShouldBe(running.Id);

        await host.RestartAsync();
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var listed = Claimed(await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token));

        listed.ShouldBe([running with { Status = OperationStatus.Running, Attempts = 1 }, pending]);
    }

    [Fact]
    public async Task retry_a_failed_operation_when_its_next_attempt_is_due_and_dead_letter_it_otherwise()
    {
        await using var host = DatabaseTestHost.Create();
        var job = await SeedJobAsync(host);
        var store = host.Services.GetRequiredService<IOperationStore>();
        var retried = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.Background, 0);
        var abandoned = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.Background, 1);
        await EnqueueAsync(store, retried, abandoned);
        Claimed(await store.ClaimNextAsync(Start.AddHours(1), 2, Token)).Count.ShouldBe(2);

        (await store.FailAsync(retried.Id, "timeout", Start.AddHours(2), Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await store.FailAsync(abandoned.Id, "access denied", null, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var tooSoon = Claimed(await store.ClaimNextAsync(Start.AddHours(1), 2, Token));
        var due = Claimed(await store.ClaimNextAsync(Start.AddHours(2), 2, Token));
        var listed = Claimed(await store.ListAsync(Token));

        tooSoon.ShouldBeEmpty();
        due.Select(operation => operation.Id).ShouldBe([retried.Id]);
        due.Single().Attempts.ShouldBe(2);
        listed.Single(operation => operation.Id == abandoned.Id).ShouldBe(abandoned with { Status = OperationStatus.DeadLetter, Attempts = 1, LastError = "access denied" });
    }

    [Fact]
    public async Task refuse_to_finish_an_operation_that_is_not_running()
    {
        await using var host = DatabaseTestHost.Create();
        var job = await SeedJobAsync(host);
        var store = host.Services.GetRequiredService<IOperationStore>();
        var pending = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.Background, 0);
        await EnqueueAsync(store, pending);

        var completed = await store.CompleteAsync(pending.Id, Token);
        var failed = await store.FailAsync(new OperationId(Guid.NewGuid()), "x", null, Token);

        completed.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new OperationNotRunning(pending.Id));
        failed.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBeOfType<OperationNotRunning>();
    }

    [Fact]
    public async Task refuse_an_operation_for_an_unknown_job()
    {
        await using var host = DatabaseTestHost.Create();
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var store = host.Services.GetRequiredService<IOperationStore>();
        var orphan = NewOperation(new SyncJobId(Guid.NewGuid()), new ItemId(Guid.NewGuid()), OperationPriority.Background, 0);

        var result = await store.EnqueueAsync(orphan, Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBeOfType<SyncJobNotFound>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DatabaseTestHost.Create();
        var job = await SeedJobAsync(host);
        var store = host.Services.GetRequiredService<IOperationStore>();
        var operation = NewOperation(job, new ItemId(Guid.NewGuid()), OperationPriority.Background, 0);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => store.EnqueueAsync(operation, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.ClaimNextAsync(Start, 1, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.CompleteAsync(operation.Id, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.FailAsync(operation.Id, "x", null, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.ListAsync(cancelled.Token));
        Claimed(await store.ListAsync(Token)).ShouldBeEmpty();
    }

    private static Operation NewOperation(SyncJobId job, ItemId item, OperationPriority priority, int ageRank)
        => new(new OperationId(Guid.NewGuid()), job, item, OperationType.UploadFile, priority, OperationStatus.Pending, 0, Start, null, Start.AddMinutes(ageRank));

    private static IReadOnlyList<Operation> Claimed(Result<IReadOnlyList<Operation>, StoreError> result)
        => result.Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value;

    private static async Task EnqueueAsync(IOperationStore store, params Operation[] operations)
    {
        foreach (var operation in operations) (await store.EnqueueAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();
    }

    private static async Task<SyncJobId> SeedJobAsync(DatabaseTestHost host)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", Start);
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), BootstrapMode.Download, true, Start, Start);
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job.Id;
    }
}
