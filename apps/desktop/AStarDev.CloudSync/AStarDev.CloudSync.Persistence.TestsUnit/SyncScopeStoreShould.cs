using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class SyncScopeStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task list_added_scopes_ordered_by_folder_id()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var store = host.Services.GetRequiredService<ISyncScopeStore>();

        await store.AddAsync(Scope(job.Id, "notes"), Token);
        await store.AddAsync(Scope(job.Id, "docs"), Token);
        var listed = await store.ListAsync(job.Id, Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncScope>>>().Value.ShouldBe([Scope(job.Id, "docs"), Scope(job.Id, "notes")]);
    }

    [Fact]
    public async Task refuse_a_folder_the_job_already_holds()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var store = host.Services.GetRequiredService<ISyncScopeStore>();
        await store.AddAsync(Scope(job.Id, "docs"), Token);

        var result = await store.AddAsync(Scope(job.Id, "docs"), Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new SyncScopeAlreadyExists(Scope(job.Id, "docs")));
    }

    [Fact]
    public async Task refuse_a_scope_for_a_job_that_is_not_stored()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var scope = Scope(new SyncJobId(Guid.NewGuid()), "docs");

        var result = await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(scope, Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new SyncJobNotFound(scope.SyncJobId));
    }

    [Fact]
    public async Task forget_a_removed_scope()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var store = host.Services.GetRequiredService<ISyncScopeStore>();
        await store.AddAsync(Scope(job.Id, "docs"), Token);
        await store.AddAsync(Scope(job.Id, "notes"), Token);

        var removed = await store.RemoveAsync(Scope(job.Id, "docs"), Token);
        var listed = await store.ListAsync(job.Id, Token);

        removed.Value.ShouldBeOfType<Ok<Unit>>();
        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncScope>>>().Value.ShouldBe([Scope(job.Id, "notes")]);
    }

    [Fact]
    public async Task report_a_folder_the_job_does_not_hold_when_removing()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await SyncJobStoreShould.AddAccountAndJobAsync(host, BootstrapMode.Merge);

        var result = await host.Services.GetRequiredService<ISyncScopeStore>().RemoveAsync(Scope(job.Id, "docs"), Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new SyncScopeNotFound(Scope(job.Id, "docs")));
    }

    [Fact]
    public async Task list_nothing_for_an_unknown_job()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);

        var listed = await host.Services.GetRequiredService<ISyncScopeStore>().ListAsync(new SyncJobId(Guid.NewGuid()), Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncScope>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<ISyncScopeStore>();
        var scope = Scope(new SyncJobId(Guid.NewGuid()), "docs");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => store.AddAsync(scope, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.RemoveAsync(scope, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.ListAsync(scope.SyncJobId, cancelled.Token));
    }

    private static SyncScope Scope(SyncJobId jobId, string folder) => new(jobId, new RemoteItemId(folder));
}
