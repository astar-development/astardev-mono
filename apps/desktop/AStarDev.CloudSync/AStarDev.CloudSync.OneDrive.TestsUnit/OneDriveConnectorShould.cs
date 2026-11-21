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
        var connector = new OneDriveConnector(store, new FakeSignIn(() => new SignInOutcome(Account, [9])));

        var result = await connector.ConnectAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Ok<ProviderAccount>>().Value.ShouldBe(Account);
        store.Saved.ShouldBe([9]);
    }

    [Fact]
    public async Task return_the_store_failure_without_signing_in()
    {
        var failure = new Fail<ProviderError>(new AuthenticationFailed("no keyring"));
        var signIn = new FakeSignIn(() => new SignInOutcome(Account, []));
        var connector = new OneDriveConnector(new FakeStore { LoadResult = failure }, signIn);

        var result = await connector.ConnectAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
        signIn.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task map_a_msal_failure_to_authentication_failed()
    {
        var connector = new OneDriveConnector(new FakeStore(), new FakeSignIn(() => throw new MsalServiceException("x", "denied")));

        var result = await connector.ConnectAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var connector = new OneDriveConnector(new FakeStore(), new FakeSignIn(() => new SignInOutcome(Account, [])));
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
