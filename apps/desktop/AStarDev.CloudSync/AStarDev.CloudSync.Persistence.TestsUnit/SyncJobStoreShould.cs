using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class SyncJobStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task return_an_added_job_with_its_bootstrap_mode()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var job = await AddAccountAndJobAsync(host, BootstrapMode.Upload);

        var found = await host.Services.GetRequiredService<ISyncJobStore>().GetAsync(job.Id, Token);
        var listed = await host.Services.GetRequiredService<ISyncJobStore>().ListAsync(Token);

        found.Value.ShouldBeOfType<Ok<SyncJob>>().Value.ShouldBe(job);
        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncJob>>>().Value.ShouldBe([job]);
    }

    [Fact]
    public async Task list_no_jobs_before_one_is_added()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);

        var listed = await host.Services.GetRequiredService<ISyncJobStore>().ListAsync(Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncJob>>>().Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task refuse_a_second_job()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var first = await AddAccountAndJobAsync(host, BootstrapMode.Merge);
        var second = first with { Id = new SyncJobId(Guid.NewGuid()) };

        var result = await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(second, Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new SyncJobAlreadyExists(first.Id));
    }

    [Fact]
    public async Task refuse_a_job_for_an_account_that_is_not_stored()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var accountId = new AccountId(Guid.NewGuid());

        var result = await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(NewJob(accountId, BootstrapMode.Merge), Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new AccountNotFound(accountId));
    }

    [Fact]
    public async Task report_an_unknown_job_as_not_found()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var id = new SyncJobId(Guid.NewGuid());

        var found = await host.Services.GetRequiredService<ISyncJobStore>().GetAsync(id, Token);

        found.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new SyncJobNotFound(id));
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<ISyncJobStore>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => store.AddAsync(NewJob(new AccountId(Guid.NewGuid()), BootstrapMode.Merge), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.GetAsync(new SyncJobId(Guid.NewGuid()), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.ListAsync(cancelled.Token));
    }

    internal static SyncJob NewJob(AccountId accountId, BootstrapMode mode)
        => new(new SyncJobId(Guid.NewGuid()), accountId, "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), mode, true, new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 21, 1, 0, 0, TimeSpan.Zero));

    internal static async Task<SyncJob> AddAccountAndJobAsync(PersistenceTestHost host, BootstrapMode mode)
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", DateTimeOffset.UnixEpoch);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        var job = NewJob(account.Id, mode);
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        return job;
    }
}
