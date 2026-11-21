using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class OneDriveDisconnectorShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task clear_the_cache_and_drop_the_token()
    {
        var store = new FakeTokenCacheStore { Cache = [1] };
        var holder = new AccessTokenHolder();
        holder.Set("token");
        var disconnector = new OneDriveDisconnector(store, holder);

        var result = await disconnector.DisconnectAsync(Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        store.Cache.ShouldBeEmpty();
        holder.Current.ShouldBeNull();
    }

    [Fact]
    public async Task keep_the_token_when_the_cache_cannot_be_cleared()
    {
        var holder = new AccessTokenHolder();
        holder.Set("token");
        var failure = new Fail<ProviderError>(new AuthenticationFailed("no keyring"));
        var disconnector = new OneDriveDisconnector(new FakeTokenCacheStore { ClearResult = failure }, holder);

        var result = await disconnector.DisconnectAsync(Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>();
        holder.Current.ShouldBe("token");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var disconnector = new OneDriveDisconnector(new FakeTokenCacheStore(), new AccessTokenHolder());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => disconnector.DisconnectAsync(cancelled.Token));
    }
}
