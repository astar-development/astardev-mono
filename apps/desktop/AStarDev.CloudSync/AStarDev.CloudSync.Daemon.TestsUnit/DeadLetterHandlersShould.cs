using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class DeadLetterHandlersShould
{
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_one_page_of_the_dead_letters_with_how_many_there_are()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var first = await SeedDeadLetterAsync(host);
        var store = host.Services.GetRequiredService<IOperationStore>();
        var others = new[] { await SeedPendingAsync(host, first.SyncJobId), await SeedPendingAsync(host, first.SyncJobId) };
        (await store.ClaimNextAsync(Start.AddHours(1), 2, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value.Count.ShouldBe(2);
        foreach (var other in others)
            (await store.FailAsync(other.Id, "access denied", null, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var query = host.Services.GetRequiredService<DeadLettersQuery>();

        var pages = new List<DeadLettersView>();
        foreach (var skip in new[] { 0, 1, 2 })
            pages.Add((await query.HandleAsync(new GetDeadLetters(skip, 1), Token)).Value.ShouldBeOfType<Ok<DeadLettersView>>().Value);

        pages.Select(page => (page.Total, page.DeadLetters.Count)).ShouldBe([(3, 1), (3, 1), (3, 1)]);
        pages.Select(page => page.DeadLetters[0].OperationId).ShouldBe([first.Id.Value, .. others.Select(other => other.Id.Value)], ignoreOrder: true);
    }

    [Fact]
    public async Task list_the_dead_lettered_operations_only()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var dead = await SeedDeadLetterAsync(host);
        await SeedPendingAsync(host, dead.SyncJobId);

        var result = await host.Services.GetRequiredService<DeadLettersQuery>().HandleAsync(new GetDeadLetters(), Token);

        var view = result.Value.ShouldBeOfType<Ok<DeadLettersView>>().Value.DeadLetters.ShouldHaveSingleItem();
        view.OperationId.ShouldBe(dead.Id.Value);
        view.ItemId.ShouldBe(dead.ItemId.Value);
        view.Type.ShouldBe(nameof(OperationType.UploadFile));
        view.Attempts.ShouldBe(1);
        view.LastError.ShouldBe("access denied");
        view.CreatedUtc.ShouldBe(Start);
    }

    [Fact]
    public async Task requeue_on_retry()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var dead = await SeedDeadLetterAsync(host);

        var result = await host.Services.GetRequiredService<RetryOperationHandler>().HandleAsync(new RetryOperation(dead.Id.Value), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        var stored = await StoredAsync(host, dead.Id);
        stored.Status.ShouldBe(OperationStatus.Pending);
        stored.Attempts.ShouldBe(0);
    }

    [Fact]
    public async Task drop_on_ignore()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var dead = await SeedDeadLetterAsync(host);

        var result = await host.Services.GetRequiredService<IgnoreOperationHandler>().HandleAsync(new IgnoreOperation(dead.Id.Value), Token);

        result.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Accepted);
        (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task reject_an_operation_that_is_not_dead_lettered()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        var dead = await SeedDeadLetterAsync(host);
        var pending = await SeedPendingAsync(host, dead.SyncJobId);

        var retry = await host.Services.GetRequiredService<RetryOperationHandler>().HandleAsync(new RetryOperation(pending.Id.Value), Token);
        var ignore = await host.Services.GetRequiredService<IgnoreOperationHandler>().HandleAsync(new IgnoreOperation(Guid.NewGuid()), Token);

        retry.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Rejected);
        ignore.Value.ShouldBeOfType<Ok<CommandResult>>().Value.Outcome.ShouldBe(CommandOutcome.Rejected);
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = DaemonTestHost.CreateWith(new StubCloudProvider());
        await host.InitialiseDatabaseAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<DeadLettersQuery>().HandleAsync(new GetDeadLetters(), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<RetryOperationHandler>().HandleAsync(new RetryOperation(Guid.NewGuid()), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => host.Services.GetRequiredService<IgnoreOperationHandler>().HandleAsync(new IgnoreOperation(Guid.NewGuid()), cancelled.Token));
    }

    private static async Task<Operation> SeedDeadLetterAsync(DaemonTestHost host)
    {
        var account = await host.AddAccountAsync("ada", AccountStatus.Connected, Token);
        var job = await host.AddJobAsync(account, "/home/ada/OneDrive", Token);
        var operation = await SeedPendingAsync(host, job.Id);
        var store = host.Services.GetRequiredService<IOperationStore>();
        (await store.ClaimNextAsync(Start.AddHours(1), 1, Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value.ShouldHaveSingleItem();
        (await store.FailAsync(operation.Id, "access denied", null, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return operation;
    }

    private static async Task<Operation> SeedPendingAsync(DaemonTestHost host, SyncJobId job)
    {
        var operation = new Operation(new OperationId(Guid.NewGuid()), job, new ItemId(Guid.NewGuid()), OperationType.UploadFile, OperationPriority.Background, OperationStatus.Pending, 0, Start, null, Start);
        (await host.Services.GetRequiredService<IOperationStore>().EnqueueAsync(operation, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return operation;
    }

    private static async Task<Operation> StoredAsync(DaemonTestHost host, OperationId id)
        => (await host.Services.GetRequiredService<IOperationStore>().ListAsync(Token)).Value.ShouldBeOfType<Ok<IReadOnlyList<Operation>>>().Value.Single(operation => operation.Id == id);
}
