using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class TokenRefresherShould
{
    private const string Id = "one";

    private static readonly ProviderAccount Account = new(Id, "one@outlook.com");

    private static Task<Result<ProviderAccount, ProviderError>> Succeed(int[] calls, string _, CancellationToken __)
    {
        calls[0]++;

        return Task.FromResult<Result<ProviderAccount, ProviderError>>(new Ok<ProviderAccount>(Account));
    }

    [Fact]
    public async Task renew_a_token_that_expires_within_five_minutes()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch.AddYears(50));
        var accounts = TestAccounts.Create();
        accounts.Set(Id, "old", time.GetUtcNow().AddMinutes(4));
        var calls = new[] { 0 };

        var result = await new TokenRefresher(accounts, time).EnsureFreshAsync(Id, (id, token) => Succeed(calls, id, token), TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        calls[0].ShouldBe(1);
    }

    [Fact]
    public async Task leave_a_token_that_is_not_near_expiry()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch.AddYears(50));
        var accounts = TestAccounts.Create();
        accounts.Set(Id, "fresh", time.GetUtcNow().AddMinutes(30));
        var calls = new[] { 0 };

        await new TokenRefresher(accounts, time).EnsureFreshAsync(Id, (id, token) => Succeed(calls, id, token), TestContext.Current.CancellationToken);

        calls[0].ShouldBe(0);
    }

    [Fact]
    public async Task leave_a_token_with_no_known_expiry()
    {
        var accounts = TestAccounts.Create();
        accounts.Set(Id, "unknown");
        var calls = new[] { 0 };

        await new TokenRefresher(accounts, new ManualTimeProvider(DateTimeOffset.UnixEpoch.AddYears(50))).EnsureFreshAsync(Id, (id, token) => Succeed(calls, id, token), TestContext.Current.CancellationToken);

        calls[0].ShouldBe(0);
    }

    [Fact]
    public async Task return_the_failure_when_the_sign_in_is_refused()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch.AddYears(50));
        var accounts = TestAccounts.Create();
        accounts.Set(Id, "old", time.GetUtcNow().AddMinutes(-1));

        var result = await new TokenRefresher(accounts, time).EnsureFreshAsync(Id, (_, _) => Task.FromResult<Result<ProviderAccount, ProviderError>>(new Fail<ProviderError>(new AuthenticationFailed("refused"))), TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>();
    }

    [Fact]
    public async Task renew_once_when_calls_overlap()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch.AddYears(50));
        var accounts = TestAccounts.Create();
        accounts.Set(Id, "old", time.GetUtcNow().AddMinutes(1));
        var calls = new[] { 0 };
        var refresher = new TokenRefresher(accounts, time);

        Task<Result<ProviderAccount, ProviderError>> Renew(string id, CancellationToken token)
        {
            accounts.Set(Id, "new", time.GetUtcNow().AddHours(1));

            return Succeed(calls, id, token);
        }

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => refresher.EnsureFreshAsync(Id, Renew, TestContext.Current.CancellationToken)));

        calls[0].ShouldBe(1);
    }

    [Fact]
    public Task stop_waiting_when_cancelled()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch.AddYears(50));
        var accounts = TestAccounts.Create();
        accounts.Set(Id, "old", time.GetUtcNow().AddMinutes(1));
        accounts.For(Id).Holder.RenewalGate.Wait(TestContext.Current.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        return Should.ThrowAsync<OperationCanceledException>(() => new TokenRefresher(accounts, time).EnsureFreshAsync(Id, (_, _) => throw new InvalidOperationException(), cancelled.Token));
    }
}
