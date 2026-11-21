using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.TestsIntegration;

public sealed class FakeCloudProviderShould
{
    private static readonly SyncScope Scope = new(new RemoteItemId("root"));

    [Fact]
    public async Task page_through_a_generated_tree_of_the_requested_size()
    {
        var provider = new FakeCloudProvider(pageSize: 40);
        provider.GenerateTree(100);
        var seen = new List<ProviderItem>();
        var pageSizes = new List<int>();

        while (true)
        {
            var page = await provider.GetSnapshotAsync(Scope, TestContext.Current.CancellationToken);
            var items = page.Value.ShouldBeOfType<Ok<ProviderSnapshotPage>>().Value.Items;
            if (items.Count == 0) break;

            pageSizes.Add(items.Count);
            seen.AddRange(items);
        }

        pageSizes.ShouldBe([40, 40, 20]);
        seen.Select(item => item.RemoteItemId).Distinct().Count().ShouldBe(100);
    }

    [Fact]
    public async Task fail_as_scripted_then_succeed()
    {
        var provider = new FakeCloudProvider(pageSize: 10);
        provider.GenerateTree(3);
        provider.ScriptFailures(new Transient("network drop"), new RateLimited(TimeSpan.FromSeconds(7)), new AuthenticationFailed("expired"));

        var first = await provider.GetSnapshotAsync(Scope, TestContext.Current.CancellationToken);
        var second = await provider.GetSnapshotAsync(Scope, TestContext.Current.CancellationToken);
        var third = await provider.GetSnapshotAsync(Scope, TestContext.Current.CancellationToken);
        var fourth = await provider.GetSnapshotAsync(Scope, TestContext.Current.CancellationToken);

        first.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<Transient>();
        second.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<RateLimited>().RetryAfter.ShouldBe(TimeSpan.FromSeconds(7));
        third.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
        fourth.Value.ShouldBeOfType<Ok<ProviderSnapshotPage>>().Value.Items.Count.ShouldBe(3);
    }

    [Fact]
    public async Task connect_to_a_fake_account()
    {
        var provider = new FakeCloudProvider(pageSize: 10);

        var result = await provider.ConnectAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Ok<ProviderAccount>>().Value.AccountIdentifier.ShouldBe("fake-account");
    }

    [Fact]
    public async Task fail_connect_as_scripted()
    {
        var provider = new FakeCloudProvider(pageSize: 10);
        provider.ScriptFailures(new AuthenticationFailed("refused"));

        var result = await provider.ConnectAsync(TestContext.Current.CancellationToken);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
    }

    [Fact]
    public async Task throw_when_the_snapshot_is_cancelled()
    {
        var provider = new FakeCloudProvider(pageSize: 10);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.GetSnapshotAsync(Scope, cancellation.Token));
    }

    [Fact]
    public async Task throw_when_the_connect_is_cancelled()
    {
        var provider = new FakeCloudProvider(pageSize: 10);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.ConnectAsync(cancellation.Token));
    }
}
