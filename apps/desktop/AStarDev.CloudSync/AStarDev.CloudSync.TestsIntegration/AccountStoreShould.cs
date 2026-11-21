using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Persistence;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class AccountStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task keep_an_account_across_a_restart()
    {
        var account = new Account(new AccountId(Guid.NewGuid()), "OneDrive", "ada@example.com", "acct-1", new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero), AccountStatus.ReauthenticationRequired);
        await using var first = DatabaseTestHost.Create();
        await first.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        (await first.Services.GetRequiredService<IAccountStore>().AddAsync(account, Token)).Value.ShouldBeOfType<Ok<Unit>>();

        await first.RestartAsync();
        await first.Services.GetRequiredService<IDatabaseInitialiser>().InitialiseAsync(Token);
        var listed = await first.Services.GetRequiredService<IAccountStore>().ListAsync(Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<Account>>>().Value.ShouldBe([account]);
    }
}
