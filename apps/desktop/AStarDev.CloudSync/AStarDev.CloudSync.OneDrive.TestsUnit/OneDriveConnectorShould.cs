using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Identity.Client;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class OneDriveConnectorShould
{
    private static readonly ProviderAccount Account = new("id", "Name");

    [Fact]
    public async Task connect_and_persist_the_refreshed_cache()
    {
        var store = new FakeStore();
        var connector = new OneDriveConnector(store, new AccessTokenHolder(), new FakeSignIn(() => new SignInOutcome(Account, [9], "token")));

        var result = await connector.ConnectAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Ok<ProviderAccount>>().Value.ShouldBe(Account);
        store.Saved.ShouldBe([9]);
    }

    [Fact]
    public async Task keep_the_access_token_for_later_requests_only_after_the_cache_is_saved()
    {
        var holder = new AccessTokenHolder();
        var connector = new OneDriveConnector(new FakeStore(), holder, new FakeSignIn(() => new SignInOutcome(Account, [9], "token")));

        await connector.ConnectAsync(TestContext.Current.CancellationToken);

        holder.Current.ShouldBe("token");
    }

    [Fact]
    public async Task keep_no_access_token_when_sign_in_fails()
    {
        var holder = new AccessTokenHolder();
        var connector = new OneDriveConnector(new FakeStore(), holder, new FakeSignIn(() => throw new MsalServiceException("x", "denied")));

        await connector.ConnectAsync(TestContext.Current.CancellationToken);

        holder.Current.ShouldBeNull();
    }

    [Fact]
    public async Task return_the_store_failure_without_signing_in()
    {
        var failure = new Fail<ProviderError>(new AuthenticationFailed("no keyring"));
        var signIn = new FakeSignIn(() => new SignInOutcome(Account, [], "token"));
        var connector = new OneDriveConnector(new FakeStore { LoadResult = failure }, new AccessTokenHolder(), signIn);

        var result = await connector.ConnectAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
        signIn.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task map_a_msal_failure_to_authentication_failed()
    {
        var connector = new OneDriveConnector(new FakeStore(), new AccessTokenHolder(), new FakeSignIn(() => throw new MsalServiceException("x", "denied")));

        var result = await connector.ConnectAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var connector = new OneDriveConnector(new FakeStore(), new AccessTokenHolder(), new FakeSignIn(() => new SignInOutcome(Account, [], "token")));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => connector.ConnectAsync(cancelled.Token));
    }

    private sealed class FakeStore : ITokenCacheStore
    {
        public Result<byte[], ProviderError> LoadResult { get; init; } = new Ok<byte[]>([]);

        public byte[]? Saved { get; private set; }

        public Task<Result<byte[], ProviderError>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(LoadResult);

        public Task<Result<Unit, ProviderError>> SaveAsync(byte[] cache, CancellationToken cancellationToken)
        {
            Saved = cache;

            return Task.FromResult<Result<Unit, ProviderError>>(new Ok<Unit>(default));
        }
    }

    private sealed class FakeSignIn(Func<SignInOutcome> outcome) : IBrowserSignIn
    {
        public int Calls { get; private set; }

        public Task<SignInOutcome> SignInAsync(byte[] cache, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult(outcome());
        }
    }
}
