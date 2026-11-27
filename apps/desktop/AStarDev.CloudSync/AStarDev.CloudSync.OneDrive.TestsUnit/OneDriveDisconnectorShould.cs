using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class OneDriveDisconnectorShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task clear_the_cache_and_drop_the_token_when_no_account_remains()
    {
        var store = new FakeTokenCacheStore { Cache = [1] };
        var accounts = TestAccounts.Create();
        accounts.Set("one", "token");
        var disconnector = new OneDriveDisconnector(store, accounts, new FakeSignOut(new RemainingCache([], false)));

        var result = await disconnector.DisconnectAsync("one", Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        store.Cache.ShouldBeEmpty();
        accounts.For("one").Holder.Current.ShouldBeNull();
    }

    [Fact]
    public async Task keep_the_other_account_signed_in()
    {
        var store = new FakeTokenCacheStore { Cache = [1] };
        var accounts = TestAccounts.Create();
        accounts.Set("one", "token-one");
        accounts.Set("two", "token-two");
        var signOut = new FakeSignOut(new RemainingCache([7], true));
        var disconnector = new OneDriveDisconnector(store, accounts, signOut);

        var result = await disconnector.DisconnectAsync("one", Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        signOut.Removed.ShouldBe("one");
        store.Cache.ShouldBe([7]);
        accounts.For("one").Holder.Current.ShouldBeNull();
        accounts.For("two").Holder.Current.ShouldBe("token-two");
    }

    [Fact]
    public async Task keep_the_token_when_the_cache_cannot_be_updated()
    {
        var accounts = TestAccounts.Create();
        accounts.Set("one", "token");
        var failure = new Fail<ProviderError>(new AuthenticationFailed("no keyring"));
        var disconnector = new OneDriveDisconnector(new FakeTokenCacheStore { ClearResult = failure }, accounts, new FakeSignOut(new RemainingCache([], false)));

        var result = await disconnector.DisconnectAsync("one", Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>();
        accounts.For("one").Holder.Current.ShouldBe("token");
    }

    [Fact]
    public async Task honour_cancellation()
    {
        var disconnector = new OneDriveDisconnector(new FakeTokenCacheStore(), TestAccounts.Create(), new FakeSignOut(new RemainingCache([], false)));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => disconnector.DisconnectAsync("one", cancelled.Token));
    }

    private sealed class FakeSignOut(RemainingCache remaining) : ISignOut
    {
        public string? Removed { get; private set; }

        public Task<RemainingCache> RemoveAsync(byte[] cache, string accountIdentifier, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Removed = accountIdentifier;

            return Task.FromResult(remaining);
        }
    }
}
