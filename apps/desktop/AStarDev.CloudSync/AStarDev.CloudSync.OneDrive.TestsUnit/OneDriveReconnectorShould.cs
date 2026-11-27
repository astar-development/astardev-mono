using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Identity.Client;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class OneDriveReconnectorShould
{
    private static readonly ProviderAccount Account = new("id", "Name");
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task sign_in_from_the_stored_cache_and_save_the_refreshed_one()
    {
        var store = new FakeTokenCacheStore { Cache = [1] };
        var accounts = TestAccounts.Create();
        var signIn = new FakeSilentSignIn(() => new SignInOutcome(Account, [2], "token"));
        var reconnector = new OneDriveReconnector(store, accounts, signIn);

        var result = await reconnector.ReconnectAsync("id", Token);

        result.Value.ShouldBeOfType<Ok<ProviderAccount>>().Value.ShouldBe(Account);
        signIn.Requested.ShouldNotBeNull().AccountIdentifier.ShouldBe("id");
        signIn.Requested.ShouldNotBeNull().Cache.ShouldBe([1]);
        store.Cache.ShouldBe([2]);
        accounts.For("id").Holder.Current.ShouldBe("token");
    }

    [Fact]
    public async Task report_authentication_failed_when_the_user_must_sign_in_again()
    {
        var store = new FakeTokenCacheStore { Cache = [1] };
        var accounts = TestAccounts.Create();
        var reconnector = new OneDriveReconnector(store, accounts, new FakeSilentSignIn(() => throw new MsalUiRequiredException("invalid_grant", "expired")));

        var result = await reconnector.ReconnectAsync("id", Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
        store.Cache.ShouldBe([1]);
        accounts.For("id").Holder.Current.ShouldBeNull();
    }

    [Fact]
    public async Task report_transient_when_the_provider_cannot_be_reached()
    {
        var reconnector = new OneDriveReconnector(new FakeTokenCacheStore(), TestAccounts.Create(), new FakeSilentSignIn(() => throw new HttpRequestException("offline")));

        var result = await reconnector.ReconnectAsync("id", Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Transient>();
    }

    [Fact]
    public async Task return_the_store_failure_without_signing_in()
    {
        var signIn = new FakeSilentSignIn(() => new SignInOutcome(Account, [], "token"));
        var failure = new Fail<ProviderError>(new AuthenticationFailed("no keyring"));
        var reconnector = new OneDriveReconnector(new FakeTokenCacheStore { LoadResult = failure }, TestAccounts.Create(), signIn);

        var result = await reconnector.ReconnectAsync("id", Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>().Message.ShouldBe("no keyring");
        signIn.Requested.ShouldBeNull();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var reconnector = new OneDriveReconnector(new FakeTokenCacheStore(), TestAccounts.Create(), new FakeSilentSignIn(() => new SignInOutcome(Account, [], "token")));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => reconnector.ReconnectAsync("id", cancelled.Token));
    }

    private sealed class FakeSilentSignIn(Func<SignInOutcome> outcome) : ISilentSignIn
    {
        public (string AccountIdentifier, byte[] Cache)? Requested { get; private set; }

        public Task<SignInOutcome> AcquireAsync(byte[] cache, string accountIdentifier, CancellationToken cancellationToken)
        {
            Requested = (accountIdentifier, cache);

            return Task.FromResult(outcome());
        }
    }
}
