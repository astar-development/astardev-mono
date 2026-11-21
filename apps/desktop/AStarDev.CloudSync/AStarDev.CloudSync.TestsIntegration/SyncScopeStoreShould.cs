using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class SyncScopeStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task keep_scopes_across_a_restart()
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero));
        var job = new SyncJob(new SyncJobId(Guid.NewGuid()), account.Id, "OneDrive", "/home/ada/OneDrive", new RemoteItemId("root"), BootstrapMode.Download, true, account.CreatedUtc, account.CreatedUtc);
        var documents = new SyncScope(job.Id, new RemoteItemId("documents"));
        var notes = new SyncScope(job.Id, new RemoteItemId("notes"));
        await using var host = DatabaseTestHost.Create();
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await host.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncJobStore>().AddAsync(job, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(documents, Token)).Value.ShouldBeOfType<Ok<Unit>>();
        (await host.Services.GetRequiredService<ISyncScopeStore>().AddAsync(notes, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        await host.RestartAsync();
        await host.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var jobs = await host.Services.GetRequiredService<ISyncJobStore>().ListAsync(Token);
        var scopes = await host.Services.GetRequiredService<ISyncScopeStore>().ListAsync(job.Id, Token);

        jobs.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncJob>>>().Value.ShouldBe([job]);
        scopes.Value.ShouldBeOfType<Ok<IReadOnlyList<SyncScope>>>().Value.ShouldBe([documents, notes]);
    }
}
