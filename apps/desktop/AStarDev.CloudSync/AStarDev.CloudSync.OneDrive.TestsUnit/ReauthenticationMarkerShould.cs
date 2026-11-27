using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging.Abstractions;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class ReauthenticationMarkerShould
{
    private static readonly Account Stored = new(new AccountId(Guid.NewGuid()), "OneDrive", "one@outlook.com", "one", DateTimeOffset.UnixEpoch);

    private static ReauthenticationMarker Create(FakeAccountStore store) => new(store, NullLogger<ReauthenticationMarker>.Instance);

    [Fact]
    public async Task require_reauthentication_for_the_matching_account()
    {
        var other = Stored with { Id = new AccountId(Guid.NewGuid()), AccountIdentifier = "two" };
        var store = new FakeAccountStore(Stored, other);

        await Create(store).MarkAsync("one", TestContext.Current.CancellationToken);

        store.Accounts.Single(account => account.AccountIdentifier == "one").Status.ShouldBe(AccountStatus.ReauthenticationRequired);
        store.Accounts.Single(account => account.AccountIdentifier == "two").Status.ShouldBe(AccountStatus.Connected);
    }

    [Fact]
    public async Task leave_an_unknown_account_alone()
    {
        var store = new FakeAccountStore(Stored);

        await Create(store).MarkAsync("nobody", TestContext.Current.CancellationToken);

        store.Accounts.Single().Status.ShouldBe(AccountStatus.Connected);
    }

    [Fact]
    public async Task not_throw_when_the_store_fails()
    {
        var store = new FakeAccountStore(Stored) { UpdateResult = new Fail<StoreError>(new StorageFailed("disk")) };

        await Create(store).MarkAsync("one", TestContext.Current.CancellationToken);

        store.Accounts.Single().Status.ShouldBe(AccountStatus.Connected);
    }

    [Fact]
    public async Task mark_the_account_when_a_call_is_refused_with_authentication_failure()
    {
        var store = new FakeAccountStore(Stored);
        var marker = Create(store);
        using var telemetry = new OneDriveTelemetry();
        using var client = new HttpClient(new FakeGraphHandler(_ => FakeGraphHandler.Json("{}", System.Net.HttpStatusCode.Unauthorized))) { BaseAddress = new Uri("https://graph.test/v1.0/") };
        var accounts = TestAccounts.For(client, telemetry);
        var provider = new OneDriveProvider(Microsoft.Extensions.Options.Options.Create(new OneDriveSettings()), new FakeTokenCacheStore(), accounts, TimeProvider.System, marker, NullLogger<OneDriveProvider>.Instance);

        var result = await provider.GetDriveInfoAsync("one", TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>();
        store.Accounts.Single().Status.ShouldBe(AccountStatus.ReauthenticationRequired);
    }
}
